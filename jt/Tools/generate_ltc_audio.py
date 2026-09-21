"""Original procedural audio, no downloaded samples. Run from any directory."""
import math
import wave
from array import array
from pathlib import Path

RATE = 44100
OUT = Path(__file__).resolve().parents[1] / 'Assets/Resources/LTCAudio'
OUT.mkdir(parents=True, exist_ok=True)

def write(name, data):
    peak = max(abs(v) for v in data) or 1
    gain = min(1, .72 / peak)
    pcm = array('h', (round(max(-1, min(1, v * gain)) * 32767) for v in data))
    with wave.open(str(OUT / (name + '.wav')), 'wb') as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(RATE)
        f.writeframes(pcm.tobytes())
    print(name, 'seconds=', len(data)/RATE, 'peak=', round(peak*gain, 3))

beat = 60/76
length = 32 * beat
music = [0.0] * round(length * RATE)
def note(midi, start, duration, volume, soft=False):
    frequency = 440 * 2 ** ((midi-69)/12)
    for j in range(round(duration*RATE)):
        t = j/RATE
        attack = min(1, t/.025)
        release = min(1, (duration-t)/.18)
        env = attack * release * math.exp(-t/(1.8 if soft else .65))
        phase = 2*math.pi*frequency*t
        sample = math.sin(phase) + .16*math.sin(2*phase) + .035*math.sin(3*phase)
        music[(round(start*RATE)+j) % len(music)] += sample * env * volume

chords = [(48,52,55),(43,47,50),(45,48,52),(41,45,48),
          (48,52,55),(45,48,52),(41,45,48),(43,47,50)]
melody = [(72,76,79,76),(71,74,79,74),(72,76,81,76),(69,72,77,72),
          (76,79,84,79),(76,72,69,72),(72,69,65,69),(71,74,79,74)]
for bar, chord in enumerate(chords):
    for n in chord:
        note(n, bar*4*beat, 4.4*beat, .045, True)
    for k, n in enumerate(melody[bar]):
        note(n, (bar*4+k)*beat, 1.5*beat, .09 if k%2==0 else .06)
write('GardenLoop', music)
click = []
for i in range(round(.105*RATE)):
    t=i/RATE
    env=min(1,t/.005)*max(0,1-t/.105)**3
    click.append(.34*env*(math.sin(2*math.pi*660*t)+.18*math.sin(2*math.pi*990*t)))
write('ButtonTap', click)
