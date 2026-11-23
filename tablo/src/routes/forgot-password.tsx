import { createFileRoute, Link } from "@tanstack/react-router";
import { useEffect, useState } from "react";
import { ArrowLeft, ChefHat, Eye, EyeOff, Loader2, QrCode } from "lucide-react";
import { toast } from "sonner";
import { apiClient } from "@/services/apiClient";

const primaryAction = "inline-flex w-full items-center justify-center gap-2 rounded-full bg-emerald-800 px-4 py-3 text-sm font-semibold text-white hover:bg-emerald-900 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-emerald-800 disabled:opacity-60";
const RESEND_WAIT_SECONDS = 60;

export const Route = createFileRoute("/forgot-password")({
  head: () => ({
    meta: [{ title: "Forgot Password — Tablo" }],
  }),
  component: ForgotPasswordPage,
});

function ForgotPasswordPage() {
  const [step, setStep] = useState<1 | 2 | 3 | 4>(1);
  const [busy, setBusy] = useState(false);

  // State
  const [email, setEmail] = useState("");
  const [otp, setOtp] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showNewPassword, setShowNewPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [resetToken, setResetToken] = useState("");

  const [resendCooldown, setResendCooldown] = useState(0);
  const [resendAt, setResendAt] = useState(0);

  useEffect(() => {
    if (step !== 2 || resendAt === 0) return;
    const timer = window.setInterval(() => {
      const remaining = Math.max(0, Math.ceil((resendAt - Date.now()) / 1000));
      setResendCooldown(remaining);
      if (remaining === 0) window.clearInterval(timer);
    }, 250);
    return () => window.clearInterval(timer);
  }, [step, resendAt]);

  const resendTime = `${Math.floor(resendCooldown / 60)}:${String(resendCooldown % 60).padStart(2, "0")}`;

  const handleRequestOtp = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    try {
      await apiClient<{ message: string }>("/auth/forgot-password", {
        method: "POST",
        body: JSON.stringify({ email }),
      });
      setOtp("");
      setStep(2);
      setResendCooldown(RESEND_WAIT_SECONDS);
      setResendAt(Date.now() + RESEND_WAIT_SECONDS * 1000);
    } catch {
      toast.error("Could not request a code. Please try again.");
    } finally {
      setBusy(false);
    }
  };

  const handleVerifyOtp = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    try {
      const res = await apiClient<{ resetToken: string }>("/auth/verify-reset-otp", {
        method: "POST",
        body: JSON.stringify({ email, otp }),
      });
      setOtp("");
      setResetToken(res.resetToken);
      setStep(3);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Invalid OTP");
    } finally {
      setBusy(false);
    }
  };

  const handleResetPassword = async (e: React.FormEvent) => {
    e.preventDefault();
    if (newPassword !== confirmPassword) {
      toast.error("Passwords do not match");
      return;
    }
    setBusy(true);
    try {
      await apiClient<{ message: string }>("/auth/reset-password", {
        method: "POST",
        body: JSON.stringify({ email, resetToken, newPassword }),
      });
      setResetToken("");
      setNewPassword("");
      setConfirmPassword("");
      setStep(4);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Something went wrong");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="min-h-screen bg-emerald-50 px-4 py-8 text-slate-950 sm:py-10">
      <div className="mx-auto max-w-md">
        <Link to="/" className="mb-6 inline-flex items-center gap-2 font-display text-lg font-bold">
          <span className="grid h-9 w-9 place-items-center rounded-xl bg-emerald-800 text-white shadow-soft">
            <QrCode className="h-4 w-4" />
          </span>
          Tablo<span className="text-primary">.</span>
        </Link>
        <div className="rounded-3xl border border-emerald-100 bg-white p-6 shadow-card sm:p-8">
          <div className="mb-6 flex items-center gap-3">
            {step !== 2 && (
              <span className="grid h-11 w-11 shrink-0 place-items-center rounded-2xl bg-emerald-100 text-emerald-800">
                <ChefHat className="h-5 w-5" />
              </span>
            )}
            <div>
              <h1 className="font-display text-2xl font-bold">
                {step === 1 && "Forgot your password?"}
                {step === 2 && "Verify your email"}
                {step === 3 && "Create a new password"}
                {step === 4 && "Password reset"}
              </h1>
              <p className="text-sm text-muted-foreground">
                {step === 1 && "Enter the email address associated with your Tablo account and we'll send you a verification code."}
                {step === 2 && "Enter the 6-digit code from your email."}
                {step === 3 && "Your new password must be at least 6 characters long."}
                {step === 4 && "Your password has been updated."}
              </p>
            </div>
          </div>

          {step === 1 && (
            <form onSubmit={handleRequestOtp} className="space-y-4">
              <div>
                <label htmlFor="reset-email" className="mb-1 block text-xs font-semibold uppercase tracking-wider text-slate-600">Email</label>
                <input
                  id="reset-email"
                  required
                  type="email"
                  autoComplete="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  className="w-full rounded-xl border border-border bg-background px-4 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
                  placeholder="you@restaurant.com"
                />
              </div>
              <button
                type="submit"
                disabled={busy}
                  className={primaryAction}
              >
                {busy && <Loader2 className="h-4 w-4 animate-spin" />}
                  Send verification code
              </button>
            </form>
          )}

          {step === 2 && (
            <form onSubmit={handleVerifyOtp} className="space-y-4">
              <div>
                <label htmlFor="reset-otp" className="sr-only">Verification code</label>
                <input
                  id="reset-otp"
                  required
                  type="text"
                  inputMode="numeric"
                  autoComplete="one-time-code"
                  maxLength={6}
                  value={otp}
                  onChange={(e) => setOtp(e.target.value.replace(/\D/g, ""))}
                  className="w-full rounded-xl border border-emerald-200 bg-white px-4 py-3 text-center font-mono text-xl tracking-[0.35em] text-emerald-950 focus:outline-none focus:ring-2 focus:ring-emerald-700"
                />
              </div>
              <button
                type="submit"
                disabled={busy || otp.length !== 6}
                className={primaryAction}
              >
                {busy && <Loader2 className="h-4 w-4 animate-spin" />}
                Verify
              </button>

              <div className="text-center text-sm">
                <button
                  type="button"
                  disabled={busy || resendCooldown > 0}
                  onClick={handleRequestOtp}
                  className="font-medium tabular-nums text-emerald-800 enabled:hover:underline disabled:text-slate-600"
                >
                  {resendCooldown > 0 ? `Resend in ${resendTime}` : "Resend code"}
                </button>
              </div>
            </form>
          )}

          {step === 3 && (
            <form onSubmit={handleResetPassword} className="space-y-4">
              <div>
                <label htmlFor="reset-password" className="mb-1 block text-xs font-semibold uppercase tracking-wider text-slate-600">New Password</label>
                <div className="relative">
                  <input
                    id="reset-password"
                    required
                    autoComplete="new-password"
                    type={showNewPassword ? "text" : "password"}
                    minLength={6}
                    value={newPassword}
                    onChange={(e) => setNewPassword(e.target.value)}
                    className="w-full rounded-xl border border-border bg-background pl-4 pr-11 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
                    placeholder="••••••••"
                  />
                  <button
                    type="button"
                    onClick={() => setShowNewPassword(!showNewPassword)}
                    className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground transition p-1"
                    aria-label={showNewPassword ? "Hide password" : "Show password"}
                  >
                    {showNewPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                  </button>
                </div>
              </div>
              <div>
                <label htmlFor="confirm-reset-password" className="mb-1 block text-xs font-semibold uppercase tracking-wider text-slate-600">Confirm Password</label>
                <div className="relative">
                  <input
                    id="confirm-reset-password"
                    required
                    autoComplete="new-password"
                    type={showConfirmPassword ? "text" : "password"}
                    minLength={6}
                    value={confirmPassword}
                    onChange={(e) => setConfirmPassword(e.target.value)}
                    className="w-full rounded-xl border border-border bg-background pl-4 pr-11 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-ring"
                    placeholder="••••••••"
                  />
                  <button
                    type="button"
                    onClick={() => setShowConfirmPassword(!showConfirmPassword)}
                    className="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground transition p-1"
                    aria-label={showConfirmPassword ? "Hide password" : "Show password"}
                  >
                    {showConfirmPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                  </button>
                </div>
              </div>
              <button
                type="submit"
                disabled={busy}
                className={primaryAction}
              >
                {busy && <Loader2 className="h-4 w-4 animate-spin" />}
                Reset Password
              </button>
            </form>
          )}

          {step === 4 && (
            <div className="space-y-6 text-center">
              <p className="text-sm text-muted-foreground">
                Your password has been successfully reset. You can now sign in with your new password.
              </p>
              <Link
                to="/auth"
                className={primaryAction}
              >
                Back to Login
              </Link>
            </div>
          )}

          {step < 4 && (
            <div className="mt-6 flex justify-center">
              <Link
                to="/auth"
                className="inline-flex items-center gap-2 text-sm font-semibold text-muted-foreground hover:text-foreground"
              >
                <ArrowLeft className="h-4 w-4" />
                Back to login
              </Link>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
