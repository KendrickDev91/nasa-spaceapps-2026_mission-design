"""Genera la voz en off (edge-tts, voces neuronales) y los tiempos de cada palabra para los subtítulos.
uso: python tts.py <guion.json> <carpeta_salida>
Salida: <id>.mp3 y <id>.json ({"text", "words": [{"w", "t0", "t1"}]}, segundos).
"""
import asyncio
import json
import os
import sys

import edge_tts

TICKS = 10_000_000  # edge-tts informa tiempos en unidades de 100 ns


async def synth(line_id, text, voice, rate, out):
    com = edge_tts.Communicate(text, voice, rate=rate, boundary="WordBoundary")
    words = []
    with open(os.path.join(out, line_id + ".mp3"), "wb") as f:
        async for chunk in com.stream():
            if chunk["type"] == "audio":
                f.write(chunk["data"])
            elif chunk["type"] == "WordBoundary":
                t0 = chunk["offset"] / TICKS
                words.append({"w": chunk["text"], "t0": round(t0, 3), "t1": round(t0 + chunk["duration"] / TICKS, 3)})
    with open(os.path.join(out, line_id + ".json"), "w", encoding="utf-8") as f:
        json.dump({"text": text, "rate": rate, "voice": voice, "words": words}, f, ensure_ascii=False, indent=1)
    print(line_id, len(words), "palabras", words[-1]["t1"] if words else 0, "s")


async def main():
    script, out = sys.argv[1], sys.argv[2]
    os.makedirs(out, exist_ok=True)
    g = json.load(open(script, encoding="utf-8"))
    for line_id, text in g["lines"].items():
        done = os.path.join(out, line_id + ".json")
        prev = json.load(open(done, encoding="utf-8")) if os.path.exists(done) else {}
        if prev.get("text") == text and prev.get("rate") == g["rate"] and prev.get("voice") == g["voice"]:
            continue                                   # ya generada con el mismo texto, voz y velocidad
        for attempt in range(6):                       # el servicio a veces limita: reintentos con espera creciente
            try:
                await synth(line_id, text, g["voice"], g["rate"], out)
                break
            except edge_tts.exceptions.NoAudioReceived:
                wait = 5 * (attempt + 1)
                print(line_id, "sin audio, reintento en", wait, "s")
                await asyncio.sleep(wait)
        else:
            raise SystemExit(f"No se pudo generar {line_id}")


asyncio.run(main())
