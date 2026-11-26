# Biberanimationen für Fehlerseiten

HyperFrames-Komposition mit den drei tatsächlich in der App verwendeten animierten WebP-Figuren (links Suche, Mitte Reparatur, rechts Warten). Das bestehende Biberbild bleibt die Grundlage; keine zusätzliche Laufzeitbibliothek im Frontend.

Vom Repository-Stamm:

```powershell
node docs/design/hyperframes-biber/sync.mjs
npx hyperframes check docs/design/hyperframes-biber --json
npx hyperframes snapshot docs/design/hyperframes-biber --at 0,1.4,3.3
npx hyperframes preview docs/design/hyperframes-biber
```

render.py erzeugt die drei Animationen aus der Originalzeichnung mit Pillow und NumPy. Werkzeug und Pfote bewegen sich zusammen, die Bewegung geht am Schultergelenk weich in den Körper über. Bei reduzierter Bewegung zeigt das Frontend das Originalbild.
