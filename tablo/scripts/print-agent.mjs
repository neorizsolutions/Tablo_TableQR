#!/usr/bin/env node
/**
 * TABLO Local Printing Agent & Hardware Simulator
 * 
 * Secure outbound agent for ESC/POS thermal printers.
 * - Polls cloud API over HTTPS/HTTP using authenticated X-Agent-Key
 * - Delivers ESC/POS jobs to local Network TCP printers (port 9100) or OS print queues
 * - Built-in Simulator Mode for local dev and automated test suites
 */

import net from 'node:net';
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import { execFile } from 'node:child_process';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const rawPrintScript = path.join(__dirname, 'win-raw-print.ps1');

// Parse command line arguments
const args = process.argv.slice(2);
function getArg(name, defaultValue) {
  const index = args.indexOf(name);
  if (index !== -1 && index + 1 < args.length) {
    return args[index + 1];
  }
  return defaultValue;
}

const API_URL = getArg('--api-url', process.env.TABLO_API_URL || 'http://localhost:5166').replace(/\/+$/, '');
const AGENT_KEY = getArg('--agent-key', process.env.TABLO_AGENT_KEY || '');
const POLL_INTERVAL = parseInt(getArg('--poll-interval-ms', '2000'), 10);
const MODE = getArg('--mode', process.env.TABLO_PRINT_MODE || 'simulator'); // 'simulator' or 'live'
const SIM_DIR = path.resolve(getArg('--sim-dir', './.tablo-printer-sim'));
const SIM_FAILURE = getArg('--simulate-failure', 'none'); // 'none', 'offline', 'paper_out', 'lost_ack', 'uncertain'
const ONCE = args.includes('--once'); // Single polling cycle (useful for tests)

if (!AGENT_KEY) {
  console.error('[TABLO Agent] ERROR: No agent key provided. Pass --agent-key <key> or set TABLO_AGENT_KEY.');
  console.error('[TABLO Agent] Usage: node print-agent.mjs --api-url http://localhost:5166 --agent-key <key> [--mode simulator|live]');
  process.exit(1);
}

const maskedKey = AGENT_KEY.length > 12 ? `${AGENT_KEY.substring(0, 12)}***` : '***';

// Ensure simulator output directory exists
if (MODE === 'simulator' && !fs.existsSync(SIM_DIR)) {
  fs.mkdirSync(SIM_DIR, { recursive: true });
}

console.log(`[TABLO Agent] Starting printing agent in ${MODE.toUpperCase()} mode...`);
console.log(`[TABLO Agent] Target API: ${API_URL}`);
console.log(`[TABLO Agent] Authenticated with key prefix: ${maskedKey}`);
console.log(`[TABLO Agent] Mode: ${MODE} | Sim Failure mode: ${SIM_FAILURE}`);
if (MODE === 'simulator') {
  console.log(`[TABLO Agent] Simulator directory: ${SIM_DIR}`);
}

let isRunning = true;
const knownPrinters = new Map();

async function pollJobs() {
  try {
    const res = await fetch(`${API_URL}/api/printers/agent/poll`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Agent-Key': AGENT_KEY,
      },
      body: JSON.stringify({ agentVersion: '1.0.0-node' })
    });

    if (!res.ok) {
      if (res.status === 401) {
        console.error('[TABLO Agent] Authentication failed: Invalid X-Agent-Key.');
      } else {
        console.error(`[TABLO Agent] Poll returned status ${res.status}: ${await res.text()}`);
      }
      return [];
    }

    const data = await res.json();
    return data.jobs || [];
  } catch (err) {
    console.error(`[TABLO Agent] Connection error polling API: ${err.message}`);
    return [];
  }
}

async function acknowledgeJob(jobId, claimToken, status, errorMessage = null, durationMs = 0) {
  try {
    const res = await fetch(`${API_URL}/api/printers/agent/jobs/${jobId}/ack`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Agent-Key': AGENT_KEY,
      },
      body: JSON.stringify({
        jobId,
        claimToken,
        status,
        errorMessage,
        durationMs
      })
    });
    if (!res.ok) {
      console.error(`[TABLO Agent] Ack failed for job ${jobId} (${res.status}): ${await res.text()}`);
    } else {
      console.log(`[TABLO Agent] Job ${jobId} acknowledged as ${status}.`);
    }
  } catch (err) {
    console.error(`[TABLO Agent] Network error acknowledging job ${jobId}: ${err.message}`);
  }
}

async function sendHeartbeat(printerStatuses) {
  try {
    await fetch(`${API_URL}/api/printers/agent/heartbeat`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'X-Agent-Key': AGENT_KEY,
      },
      body: JSON.stringify({
        agentVersion: '1.0.0-node',
        printerStatuses
      })
    });
  } catch (err) {
    // Non-fatal
  }
}

async function printLive(job) {
  const startTime = Date.now();
  const buffer = Buffer.from(job.payloadEscPosBase64, 'base64');

  if (job.connectionType === 'NetworkTcp') {
    if (!job.ipAddress) {
      throw new Error('Printer IP address is required for NetworkTcp connection.');
    }
    const port = job.port || 9100;

    return new Promise((resolve, reject) => {
      const socket = new net.Socket();
      socket.setTimeout(6000);
      let bytesWritten = false;

      socket.connect(port, job.ipAddress, () => {
        socket.write(buffer, () => {
          bytesWritten = true;
          socket.end();
          resolve({ durationMs: Date.now() - startTime });
        });
      });

      socket.on('error', (err) => {
        socket.destroy();
        if (bytesWritten) {
          // Bytes were dispatched to buffer, but connection failed/reset before clean close
          resolve({
            status: 'UncertainDelivery',
            error: `Socket error after buffer dispatch: ${err.message}. Physical print unconfirmed.`,
            durationMs: Date.now() - startTime
          });
        } else {
          reject(err);
        }
      });

      socket.on('timeout', () => {
        socket.destroy();
        if (bytesWritten) {
          resolve({
            status: 'UncertainDelivery',
            error: `Connection timed out after buffer dispatch. Physical print unconfirmed.`,
            durationMs: Date.now() - startTime
          });
        } else {
          reject(new Error(`Connection to printer ${job.ipAddress}:${port} timed out.`));
        }
      });
    });
  } else if (job.connectionType === 'LocalSpooler' || job.connectionType === 'Usb' || job.connectionType === 'LocalQueue' || job.connectionType === 'LocalAgentQueue') {
    const queueName = job.localQueueName || job.printerName;
    if (!queueName) {
      throw new Error(`Printer '${job.printerName}' requires a valid localQueueName for ${job.connectionType} delivery.`);
    }

    // Reject control characters or newlines in queue name
    if (/[\0\r\n]/.test(queueName)) {
      throw new Error(`Invalid characters in printer queue name: '${queueName}'.`);
    }

    const tempFile = path.join(os.tmpdir(), `tablo_kot_${job.jobId}_${Date.now()}.bin`);
    fs.writeFileSync(tempFile, buffer);

    try {
      if (process.platform === 'win32') {
        // Dispatch RAW ESC/POS bytes directly via Win32 Spooler API (pDataType = RAW)
        // using argument-based execution (no shell string concatenation or injection)
        await new Promise((resolve, reject) => {
          execFile('powershell.exe', [
            '-NoProfile',
            '-ExecutionPolicy', 'Bypass',
            '-File', rawPrintScript,
            '-PrinterName', queueName,
            '-FilePath', tempFile
          ], (error, stdout, stderr) => {
            if (error) {
              const msg = (stderr || stdout || error.message).trim();
              reject(new Error(`Windows Print Spooler RAW dispatch failed for '${queueName}': ${msg}`));
            } else {
              console.log(`[TABLO Agent] Spooled job ${job.jobId} to Windows queue '${queueName}' via Win32 Spooler (RAW datatype). Physical print exit unconfirmed without bidirectional sensor.`);
              resolve();
            }
          });
        });
      } else {
        // POSIX: RAW ESC/POS print via argument-based lp invocation
        await new Promise((resolve, reject) => {
          execFile('lp', ['-d', queueName, '-o', 'raw', tempFile], (error, stdout, stderr) => {
            if (error) {
              reject(new Error(`POSIX lp print failed for queue '${queueName}': ${stderr || error.message}`));
            } else {
              console.log(`[TABLO Agent] Spooled job ${job.jobId} to CUPS queue '${queueName}' (Dispatched to OS spooler; physical print exit unconfirmed without bidirectional sensor).`);
              resolve();
            }
          });
        });
      }
      return { durationMs: Date.now() - startTime };
    } finally {
      try { if (fs.existsSync(tempFile)) fs.unlinkSync(tempFile); } catch {}
    }
  } else {
    throw new Error(`Unsupported live connection type '${job.connectionType}'. Supported modes: NetworkTcp, LocalSpooler, LocalAgentQueue, or Simulator mode.`);
  }
}

async function printSimulator(job) {
  const startTime = Date.now();
  const buffer = Buffer.from(job.payloadEscPosBase64, 'base64');

  // Record raw ESC/POS binary
  const binFile = path.join(SIM_DIR, `job_${job.jobId}_${job.paperWidthMm}mm.bin`);
  fs.writeFileSync(binFile, buffer);

  // Record human-readable ticket
  const txtFile = path.join(SIM_DIR, `job_${job.jobId}.txt`);
  fs.writeFileSync(txtFile, job.readableText || '(No readable preview)');

  // Also maintain latest ticket for easy inspection
  fs.writeFileSync(path.join(SIM_DIR, 'latest_ticket.txt'), job.readableText || '');

  console.log(`[Simulator] Ticket recorded to ${txtFile}`);
  console.log(`[Simulator] Monospace Preview:\n${job.readableText}`);

  // Test failure simulation
  if (SIM_FAILURE === 'offline') {
    throw new Error('SIMULATED_OFFLINE: Printer network socket unreachable (host offline)');
  }
  if (SIM_FAILURE === 'paper_out') {
    throw new Error('SIMULATED_PAPER_OUT: Hardware sensor reported paper-out condition');
  }
  if (SIM_FAILURE === 'uncertain' || SIM_FAILURE === 'lost_ack') {
    return { status: 'UncertainDelivery', error: 'SIMULATED_UNCERTAIN: Connection reset after buffer dispatch; physical paper delivery unconfirmed' };
  }

  return { status: 'Printed', durationMs: Date.now() - startTime };
}

async function processJob(job) {
  console.log(`[TABLO Agent] Processing job ${job.jobId} for printer '${job.printerName}' (${job.paperWidthMm}mm)...`);

  knownPrinters.set(job.printerId, { status: 'Online' });

  try {
    let result;
    if (MODE === 'simulator') {
      result = await printSimulator(job);
    } else {
      result = await printLive(job);
    }

    if (result && result.status === 'UncertainDelivery') {
      await acknowledgeJob(job.jobId, job.claimToken, 'UncertainDelivery', result.error, result.durationMs || 100);
    } else {
      await acknowledgeJob(job.jobId, job.claimToken, 'Completed', null, result?.durationMs || 50);
    }
  } catch (err) {
    console.error(`[TABLO Agent] Print failed for job ${job.jobId}: ${err.message}`);
    knownPrinters.set(job.printerId, { status: 'Error', message: err.message });
    await acknowledgeJob(job.jobId, job.claimToken, 'Failed', err.message, 0);
  }
}

async function cycle() {
  const jobs = await pollJobs();
  for (const job of jobs) {
    await processJob(job);
  }

  // Heartbeat printer statuses
  if (knownPrinters.size > 0) {
    const list = Array.from(knownPrinters.entries()).map(([printerId, info]) => ({
      printerId,
      status: info.status,
      statusMessage: info.message
    }));
    await sendHeartbeat(list);
  }
}

async function main() {
  if (ONCE) {
    await cycle();
    console.log('[TABLO Agent] Single cycle complete.');
    process.exit(0);
  }

  while (isRunning) {
    await cycle();
    await new Promise((r) => setTimeout(r, POLL_INTERVAL));
  }
}

process.on('SIGINT', () => {
  console.log('\n[TABLO Agent] Shutting down gracefully...');
  isRunning = false;
  process.exit(0);
});

process.on('SIGTERM', () => {
  isRunning = false;
  process.exit(0);
});

main().catch((err) => {
  console.error('[TABLO Agent] Fatal error:', err);
  process.exit(1);
});
