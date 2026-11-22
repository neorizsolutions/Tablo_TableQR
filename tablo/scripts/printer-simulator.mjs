#!/usr/bin/env node
/**
 * TABLO Virtual ESC/POS Network Printer Simulator
 * 
 * Simulates a physical thermal receipt printer listening on TCP port 9100 (or custom port).
 * Receives raw ESC/POS binary payloads, decodes human-readable text,
 * saves .bin and .txt files to output directory, and optionally simulates
 * hardware failure modes (offline, socket-reset, lost-ack).
 */

import net from 'node:net';
import fs from 'node:fs';
import path from 'node:path';

// Parse arguments
const args = process.argv.slice(2);
function getArg(name, defaultValue) {
  const index = args.indexOf(name);
  if (index !== -1 && index + 1 < args.length) {
    return args[index + 1];
  }
  return defaultValue;
}

const PORT = parseInt(getArg('--port', process.env.SIMULATOR_PORT || '9100'), 10);
const HOST = getArg('--host', '0.0.0.0');
const OUTPUT_DIR = path.resolve(getArg('--dir', './.tablo-printer-sim'));
const SIM_MODE = getArg('--simulate', 'none'); // 'none', 'reset_mid_stream', 'hang', 'refuse'

if (!fs.existsSync(OUTPUT_DIR)) {
  fs.mkdirSync(OUTPUT_DIR, { recursive: true });
}

console.log('='.repeat(60));
console.log('  TABLO VIRTUAL ESC/POS THERMAL PRINTER SIMULATOR');
console.log('='.repeat(60));
console.log(`[Simulator] Listening on ${HOST}:${PORT}`);
console.log(`[Simulator] Output Directory: ${OUTPUT_DIR}`);
console.log(`[Simulator] Simulation Mode: ${SIM_MODE}`);
console.log('='.repeat(60));

let ticketCounter = 0;

function cleanEscPosText(buffer) {
  // Extract ASCII/UTF-8 readable text while stripping common ESC/POS commands
  let text = '';
  let i = 0;
  while (i < buffer.length) {
    const b = buffer[i];
    if (b === 0x1B) { // ESC
      const cmd = buffer[i + 1];
      if (cmd === 0x40) { i += 2; continue; } // Init ESC @
      if (cmd === 0x61 || cmd === 0x45 || cmd === 0x4D || cmd === 0x74) { i += 3; continue; } // ESC a n, ESC E n
      if (cmd === 0x21) { i += 3; continue; } // ESC ! n
      i += 2;
      continue;
    }
    if (b === 0x1D) { // GS
      const cmd = buffer[i + 1];
      if (cmd === 0x56) { i += 4; continue; } // GS V m n (Cut)
      if (cmd === 0x21 || cmd === 0x42) { i += 3; continue; } // GS ! n, GS B n
      i += 2;
      continue;
    }
    if (b === 0x0A) { // Newline
      text += '\n';
      i++;
      continue;
    }
    if (b === 0x0D) { // Carriage return
      i++;
      continue;
    }
    if (b >= 0x20 && b <= 0x7E) {
      text += String.fromCharCode(b);
    } else if (b >= 0x80) {
      text += buffer.toString('utf8', i, i + 1);
    }
    i++;
  }
  return text.trim();
}

const server = net.createServer((socket) => {
  ticketCounter++;
  const currentTicket = ticketCounter;
  const clientInfo = `${socket.remoteAddress}:${socket.remotePort}`;
  console.log(`\n[Simulator] Connection established from ${clientInfo} (Job #${currentTicket})`);

  if (SIM_MODE === 'refuse') {
    console.log(`[Simulator] Simulation Mode: Refusing connection.`);
    socket.destroy();
    return;
  }

  const chunks = [];

  socket.on('data', (data) => {
    chunks.push(data);
    console.log(`[Simulator] Received chunk: ${data.length} bytes from ${clientInfo}`);

    if (SIM_MODE === 'reset_mid_stream') {
      console.log(`[Simulator] Simulating premature socket reset (ECONNRESET) mid-stream...`);
      socket.destroy(new Error('Simulated ECONNRESET'));
      return;
    }
  });

  socket.on('end', () => {
    if (SIM_MODE === 'hang') {
      console.log(`[Simulator] Simulating hanging connection without acknowledgement...`);
      return;
    }

    const fullBuffer = Buffer.concat(chunks);
    console.log(`[Simulator] Transfer complete. Total bytes received: ${fullBuffer.length}`);

    const timestamp = new Date().toISOString().replace(/[:.]/g, '-');
    const binFile = path.join(OUTPUT_DIR, `job_${currentTicket}_${timestamp}.bin`);
    const txtFile = path.join(OUTPUT_DIR, `job_${currentTicket}_${timestamp}.txt`);
    const latestTxt = path.join(OUTPUT_DIR, 'latest_ticket.txt');

    fs.writeFileSync(binFile, fullBuffer);

    const readable = cleanEscPosText(fullBuffer);
    fs.writeFileSync(txtFile, readable);
    fs.writeFileSync(latestTxt, readable);

    console.log(`[Simulator] Saved raw binary payload to: ${binFile}`);
    console.log(`[Simulator] Saved decoded receipt to:    ${txtFile}`);
    console.log('-'.repeat(50));
    console.log(readable);
    console.log('-'.repeat(50));
  });

  socket.on('error', (err) => {
    console.log(`[Simulator] Socket error on connection ${clientInfo}: ${err.message}`);
  });
});

server.listen(PORT, HOST, () => {
  console.log(`[Simulator] Ready to receive print jobs.`);
});

export { server, cleanEscPosText };
