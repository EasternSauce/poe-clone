"""Render Dash's impact-free air rush. Requires NumPy; run from the repo root."""

from pathlib import Path
import wave

import numpy as np


rate = 44100
duration = 0.36
count = round(rate * duration)
time = np.arange(count) / rate
rng = np.random.default_rng(8099)
frequencies = np.fft.rfftfreq(count, 1 / rate)


def airflow(low, high):
    # Broad noise bands avoid a pitched whistle, metallic ring or bass impact.
    spectrum = np.fft.rfft(rng.standard_normal(count))
    band = (1 - np.exp(-(frequencies / low) ** 4)) * np.exp(-(frequencies / high) ** 4)
    noise = np.fft.irfft(spectrum * band, n=count)
    return noise / np.std(noise)


progress = time / duration
# A rounded swell and long taper remove the attack transient of a weapon hit.
envelope = np.sin(np.pi * progress) ** 2 * np.exp(-3 * progress)
signal = (airflow(500, 3000) * (1 - progress) + airflow(1100, 6500) * progress) * envelope
signal *= 0.48 / np.max(np.abs(signal))
pcm = np.rint(signal * 32767).astype('<i2')
path = Path('Assets/Audio/SoundLibrary/Movement/Dash_Air_Rush.wav')
with wave.open(str(path), 'wb') as output:
    output.setnchannels(1)
    output.setsampwidth(2)
    output.setframerate(rate)
    output.writeframes(pcm.tobytes())
print(f'Rendered {path}: {duration}s, peak {np.max(np.abs(signal)):.2f}')
