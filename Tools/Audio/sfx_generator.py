"""
Semi-realistic sound effect generator for The Glory Castle Raider.

Pure Python (no numpy). 44.1 kHz 16-bit mono WAVs built from layered
filtered noise, resonant (biquad) filters, inharmonic bell partials and a
small Schroeder reverb, so they sit next to recorded SFX.

Usage:
    python Tools/Audio/sfx_generator.py [output_dir]
Default output: Assets/Resources/Audio/Generated
Re-running overwrites the clips (seeded, so the result is the same each time).
File names are unchanged, so the game code picks the new versions up automatically.
"""
import math
import os
import random
import struct
import sys
import wave

SR = 44100
TAU = math.pi * 2


# ------------------------------------------------------------------
# Signal helpers
# ------------------------------------------------------------------
def ns(seconds):
    return int(SR * seconds)


def silence(seconds):
    return [0.0] * ns(seconds)


def mix_into(dst, src, offset_s=0.0, gain=1.0):
    off = int(offset_s * SR)
    if off + len(src) > len(dst):
        dst.extend([0.0] * (off + len(src) - len(dst)))
    for i, v in enumerate(src):
        dst[off + i] += v * gain
    return dst


def apply_env(sig, env):
    n = len(sig)
    return [v * env(i / n) for i, v in enumerate(sig)]


def sine_sweep(seconds, freq_fn, env=None, phase0=0.0):
    n = ns(seconds)
    out, ph = [], phase0
    for i in range(n):
        t = i / n
        ph += freq_fn(t) / SR
        out.append(math.sin(TAU * ph) * (env(t) if env else 1.0))
    return out


def saw_sweep(seconds, freq_fn, env=None, detune=(0.0,)):
    """Band-limited-ish saw: sum of a few harmonics per voice (cheap, no aliasing fizz)."""
    n = ns(seconds)
    out = [0.0] * n
    for d in detune:
        ph = random.random()
        for i in range(n):
            t = i / n
            f = freq_fn(t) * (1.0 + d)
            ph += f / SR
            v = 0.0
            k = 1
            while k <= 12 and f * k < 9000:
                v += math.sin(TAU * ph * k) / k
                k += 1
            out[i] += v * (env(t) if env else 1.0) / len(detune)
    return out


def white(seconds, rng):
    return [rng.uniform(-1.0, 1.0) for _ in range(ns(seconds))]


def brown(seconds, rng):
    out, y = [], 0.0
    for _ in range(ns(seconds)):
        y = (y + rng.uniform(-1, 1) * 0.08) * 0.995
        out.append(y)
    m = max(abs(v) for v in out) or 1.0
    return [v / m for v in out]


# RBJ biquads --------------------------------------------------------
def biquad(sig, kind, freq_fn, q=0.707):
    """kind: 'lp', 'hp', 'bp'. freq_fn may be a number or f(t_norm)."""
    out = []
    x1 = x2 = y1 = y2 = 0.0
    n = len(sig)
    static = not callable(freq_fn)
    coeffs = None
    for i, x in enumerate(sig):
        if coeffs is None or (not static and i % 32 == 0):
            f = freq_fn if static else freq_fn(i / n)
            f = max(20.0, min(f, SR * 0.45))
            w = TAU * f / SR
            cw, sw = math.cos(w), math.sin(w)
            a = sw / (2 * q)
            if kind == "lp":
                b0, b1, b2 = (1 - cw) / 2, 1 - cw, (1 - cw) / 2
            elif kind == "hp":
                b0, b1, b2 = (1 + cw) / 2, -(1 + cw), (1 + cw) / 2
            else:  # constant 0 dB peak bandpass
                b0, b1, b2 = a, 0.0, -a
            a0, a1, a2 = 1 + a, -2 * cw, 1 - a
            coeffs = (b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0)
        b0, b1, b2, a1, a2 = coeffs
        y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1, y2, y1 = x1, x, y1, y
        out.append(y)
    return out


def resonate(sig, modes):
    """Body resonance: sum of narrow bandpasses [(freq, q, gain), ...]."""
    out = [0.0] * len(sig)
    for f, q, g in modes:
        for i, v in enumerate(biquad(sig, "bp", f, q)):
            out[i] += v * g
    return out


def bell(freq, seconds, partials=((1.0, 1.0, 1.0), (2.76, 0.5, 1.6), (5.4, 0.28, 2.4), (8.93, 0.15, 3.5)),
         attack=0.003):
    """Inharmonic struck-metal partials: (ratio, gain, decay speed)."""
    n = ns(seconds)
    out = [0.0] * n
    for ratio, g, k in partials:
        f = freq * ratio
        if f > SR * 0.45:
            continue
        for i in range(n):
            t = i / SR
            a = min(1.0, t / attack)
            out[i] += math.sin(TAU * f * t) * g * a * math.exp(-t * k * 3.0 / seconds)
    return out


def reverb(sig, mix=0.25, size=1.0, damp=0.35, tail_s=1.0):
    """Small Schroeder reverb (4 damped combs + 2 allpasses)."""
    src = list(sig) + [0.0] * ns(tail_s)
    wet = [0.0] * len(src)
    for d_ms, fb in ((29.7, 0.80), (37.1, 0.78), (41.1, 0.76), (43.7, 0.74)):
        d = int(d_ms * size * SR / 1000)
        buf = [0.0] * d
        idx, lp = 0, 0.0
        for i, x in enumerate(src):
            y = buf[idx]
            lp = y * (1 - damp) + lp * damp
            buf[idx] = x + lp * fb
            idx = (idx + 1) % d
            wet[i] += y * 0.25
    for d_ms, g in ((5.0, 0.7), (1.7, 0.7)):
        d = int(d_ms * SR / 1000)
        buf = [0.0] * d
        idx = 0
        for i, x in enumerate(wet):
            b = buf[idx]
            y = -x * g + b
            buf[idx] = x + b * g
            idx = (idx + 1) % d
            wet[i] = y
    return [d * (1 - mix) + w * mix for d, w in zip(src, wet)]


def trim_tail(sig, threshold=0.0008):
    end = len(sig)
    while end > 1 and abs(sig[end - 1]) < threshold:
        end -= 1
    return sig[:min(len(sig), end + ns(0.02))]


def fade_edges(sig, in_ms=2, out_ms=30):
    a, b = ns(in_ms / 1000), ns(out_ms / 1000)
    for i in range(min(a, len(sig))):
        sig[i] *= i / a
    for i in range(min(b, len(sig))):
        sig[-1 - i] *= i / b
    return sig


def normalize(sig, peak=0.89):
    m = max(abs(v) for v in sig) or 1.0
    return [v * peak / m for v in sig]


def soft_clip(sig, drive=1.5):
    t = math.tanh(drive)
    return [math.tanh(v * drive) / t for v in sig]


def finish(sig, peak=0.89):
    sig = trim_tail(normalize(sig, 1.0))
    return fade_edges(normalize(sig, peak))


def write_wav(path, sig):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(b"".join(
            struct.pack("<h", int(max(-1.0, min(1.0, v)) * 32767)) for v in sig))
    print(f"  wrote {os.path.basename(path):28s} {len(sig) / SR:5.2f}s")


def note(midi):
    return 440.0 * 2 ** ((midi - 69) / 12)


def exp_decay(rate, attack=0.002):
    def env(t):
        return min(1.0, t / attack) * math.exp(-t * rate) if attack > 0 else math.exp(-t * rate)
    return env


# ------------------------------------------------------------------
# Clips
# ------------------------------------------------------------------
def riposte_shockwave(rng):
    """Energy gathers for a split second, then a deep concussive blast with a rushing air wave."""
    out = silence(1.6)
    hit = 0.12

    # Gather: rising filtered noise swell + rising tonal whine
    gather = biquad(white(hit + 0.02, rng), "bp", lambda t: 600 + 2600 * t * t, 1.4)
    gather = apply_env(gather, lambda t: t ** 2)
    mix_into(out, gather, 0.0, 0.35)
    whine = sine_sweep(hit + 0.02, lambda t: 300 + 700 * t, lambda t: t ** 3)
    mix_into(out, whine, 0.0, 0.12)

    # Sub boom: pitch-dropping sine, slightly saturated so it reads on small speakers
    boom = sine_sweep(0.9, lambda t: 38 + 85 * math.exp(-t * 7), exp_decay(5.0, 0.004))
    mix_into(out, soft_clip(boom, 2.0), hit, 1.0)

    # Impact crack: short bright noise transient
    crack = biquad(white(0.06, rng), "hp", 1800, 0.7)
    mix_into(out, apply_env(crack, exp_decay(60.0, 0.001)), hit, 0.55)

    # Body: lowpassed brown noise thump
    body = biquad(brown(0.5, rng), "lp", 400, 0.9)
    mix_into(out, apply_env(body, exp_decay(7.0, 0.003)), hit, 0.8)

    # Air wave: noise with a falling bandpass sweep (the shockwave rushing outward)
    wave_ = biquad(white(0.9, rng), "bp", lambda t: 3500 * math.exp(-t * 3.5) + 180, 0.9)
    wave_ = apply_env(wave_, lambda t: min(1.0, t / 0.04) * (1 - t) ** 1.8)
    mix_into(out, wave_, hit + 0.01, 0.75)

    # Energy shimmer: detuned saws falling in pitch through a closing filter (the "blue" magic)
    energy = saw_sweep(0.7, lambda t: 220 * math.exp(-t * 1.6) + 70, exp_decay(4.5, 0.01),
                       detune=(-0.012, 0.0, 0.013))
    energy = biquad(energy, "lp", lambda t: 3000 * (1 - t) + 300, 1.2)
    mix_into(out, energy, hit, 0.3)

    return finish(reverb(out, mix=0.3, size=1.3, damp=0.45, tail_s=0.9), 0.92)


def riposte_ready(rng):
    """A bright struck-metal shimmer: two blade-like bell tones and a soft airy swell."""
    out = silence(0.2)
    mix_into(out, bell(note(76), 1.3), 0.0, 0.55)      # E5
    mix_into(out, bell(note(83), 1.4), 0.07, 0.45)     # B5
    mix_into(out, bell(note(88), 1.2, attack=0.01), 0.14, 0.25)  # E6 halo

    air = biquad(white(0.6, rng), "bp", lambda t: 4000 + 3000 * t, 2.0)
    air = apply_env(air, lambda t: math.sin(math.pi * t) ** 2)
    mix_into(out, air, 0.0, 0.18)

    # A soft ringing "edge" like a blade being drawn
    ring = biquad(white(0.35, rng), "bp", 6200, 18.0)
    mix_into(out, apply_env(ring, exp_decay(8.0, 0.05)), 0.03, 0.5)

    return finish(reverb(out, mix=0.32, size=1.1, damp=0.3, tail_s=1.0), 0.8)


def orb_hum_loop(rng):
    """4-second seamless loop: warm tonal drone, slow beating, faint crackle."""
    T, xfade = 4.0, 0.5
    total = T + xfade
    n = ns(total)
    out = [0.0] * n
    # Tones chosen to beat gently against each other
    voices = ((110.0, 0.45), (110.6, 0.30), (165.0, 0.22), (220.25, 0.12), (330.0, 0.05))
    for f, g in voices:
        ph = rng.random()
        for i in range(n):
            out[i] += math.sin(TAU * (ph + f * i / SR)) * g
    # Slow breathing swell
    for i in range(n):
        t = i / SR
        out[i] *= 0.8 + 0.2 * math.sin(TAU * 0.25 * t + 0.6)

    # Airy energy hiss + sparse crackles
    hiss = biquad(white(total, rng), "bp", 2400, 1.5)
    mix_into(out, hiss, 0.0, 0.04)
    for _ in range(10):
        pos = rng.uniform(0.0, total - 0.05)
        c = biquad(white(0.03, rng), "bp", rng.uniform(2500, 6000), 4.0)
        mix_into(out, apply_env(c, exp_decay(150.0, 0.0005)), pos, rng.uniform(0.08, 0.18))

    out = biquad(out, "lp", 5000, 0.7)
    out = out[:n]

    # Crossfade the extra tail into the head so the loop point is inaudible
    loop_n, fade_n = ns(T), ns(xfade)
    result = out[:loop_n]
    for i in range(fade_n):
        g = i / fade_n
        result[i] = out[i] * g + out[loop_n + i] * (1 - g)
    return normalize(result, 0.55)


def orb_consume(rng):
    """The orb's energy is drawn in: rising whoosh, then a soft chime bloom with sparkles."""
    out = silence(1.0)
    rise = 0.28

    whoosh = biquad(white(rise, rng), "bp", lambda t: 400 + 3000 * t ** 1.5, 1.2)
    mix_into(out, apply_env(whoosh, lambda t: t ** 2.2), 0.0, 0.55)
    tone_up = sine_sweep(rise, lambda t: 220 + 440 * t ** 2, lambda t: t ** 2)
    mix_into(out, tone_up, 0.0, 0.12)

    for k, m in enumerate((64, 71, 76)):            # E4 B4 E5 bloom
        mix_into(out, bell(note(m), 1.1, attack=0.012), rise + k * 0.015, 0.4 - k * 0.08)

    for _ in range(9):                              # sparkles
        pos = rise + rng.uniform(0.0, 0.35)
        f = rng.uniform(3200, 7500)
        s = sine_sweep(0.08, lambda t, f=f: f, exp_decay(45.0, 0.002))
        mix_into(out, s, pos, rng.uniform(0.04, 0.09))

    return finish(reverb(out, mix=0.3, size=1.0, damp=0.35, tail_s=0.9), 0.85)


WOOD_MODES = ((240, 6.0, 1.0), (520, 8.0, 0.8), (1150, 10.0, 0.5), (2300, 12.0, 0.25))


def creak(rng, seconds, rate_start, rate_end, irregular=0.35):
    """Stick-slip friction: an irregular impulse train exciting wooden body resonances."""
    n = ns(seconds)
    exc = [0.0] * n
    t = 0.0
    while True:
        tn = t / seconds
        rate = rate_start + (rate_end - rate_start) * tn
        rate *= 1.0 + rng.uniform(-irregular, irregular)
        t += 1.0 / max(rate, 5.0)
        i = int(t * SR)
        if i >= n:
            break
        amp = rng.uniform(0.5, 1.0)
        for k in range(min(40, n - i)):              # short grab-and-slip pulse
            exc[i + k] += amp * math.exp(-k / 6.0) * (1 if k < 3 else -0.4)
    body = resonate(exc, WOOD_MODES)
    body = biquad(body, "hp", 90, 0.7)
    shape = lambda x: min(1.0, x / 0.12) * min(1.0, (1 - x) / 0.25)
    return apply_env(body, shape)


def latch(rng, bright=1.0):
    """Small iron latch: click transient + short metallic ring."""
    click = biquad(white(0.015, rng), "hp", 2500, 0.7)
    click = apply_env(click, exp_decay(250.0, 0.0005))
    ring = bell(1850 * bright, 0.18, partials=((1.0, 1.0, 2.0), (2.1, 0.6, 3.0), (3.7, 0.3, 4.0)), attack=0.0008)
    out = list(click)
    mix_into(out, ring, 0.0, 0.25)
    return out


def door_open(rng):
    """Latch lifts, then a long uneven creak of old hinges and wood."""
    out = silence(1.2)
    mix_into(out, latch(rng), 0.0, 0.7)
    mix_into(out, creak(rng, 0.85, 45, 120, 0.4), 0.06, 1.0)
    mix_into(out, creak(rng, 0.25, 140, 190, 0.25), 0.72, 0.35)   # last squeak as it swings free
    # Air movement of the heavy door
    air = biquad(brown(0.8, rng), "lp", 300, 0.7)
    mix_into(out, apply_env(air, lambda t: math.sin(math.pi * t)), 0.1, 0.25)
    return finish(reverb(out, mix=0.2, size=0.8, damp=0.5, tail_s=0.6), 0.85)


def door_close(rng):
    """Short creak, heavy wooden slam against the frame, latch catches."""
    out = silence(1.0)
    mix_into(out, creak(rng, 0.28, 110, 70, 0.3), 0.0, 0.5)
    hit = 0.24

    thump = sine_sweep(0.45, lambda t: 55 + 50 * math.exp(-t * 10), exp_decay(9.0, 0.002))
    mix_into(out, soft_clip(thump, 1.6), hit, 1.0)

    impact = [0.0] * ns(0.5)
    for k in range(60):
        impact[k] = math.exp(-k / 10.0) * rng.uniform(-1, 1)
    knock = resonate(impact, ((140, 5.0, 1.2), (310, 6.0, 1.0), (690, 8.0, 0.6), (1450, 10.0, 0.3)))
    mix_into(out, knock, hit, 1.4)

    slap = biquad(white(0.12, rng), "lp", 1800, 0.7)
    mix_into(out, apply_env(slap, exp_decay(35.0, 0.001)), hit, 0.5)

    mix_into(out, latch(rng, 0.9), hit + 0.05, 0.55)
    return finish(reverb(out, mix=0.25, size=1.0, damp=0.5, tail_s=0.8), 0.9)


CLIPS = {
    "SFX_RiposteShockwave": riposte_shockwave,
    "SFX_RiposteReady": riposte_ready,
    "SFX_EnergyOrb_HumLoop": orb_hum_loop,
    "SFX_EnergyOrb_Consume": orb_consume,
    "SFX_Door_Open": door_open,
    "SFX_Door_Close": door_close,
}


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    project = os.path.abspath(os.path.join(here, "..", ".."))
    out_dir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
        project, "Assets", "Resources", "Audio", "Generated")
    only = set(sys.argv[2:])
    print(f"Writing semi-realistic SFX to {out_dir}")
    for name, fn in CLIPS.items():
        if only and name not in only:
            continue
        rng = random.Random(name)
        random.seed(name)       # saw_sweep phases
        write_wav(os.path.join(out_dir, name + ".wav"), fn(rng))


if __name__ == "__main__":
    main()
