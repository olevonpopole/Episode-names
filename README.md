# Episode-names
Program to rename your files. Anisearch functions (search and get episode names) included

Funktionen:
Number: Durchnummerieren der Dateien

Anisearch: Episodenliste wird von Anisearch geholt. Anisearch-Suche ist im Programm integriert.

Get-Filenames: Damit werden die Dateinamen aus dem angegebenen Ordner geholt. (Nützlich wenn man nur bestimmte Teile des Namens benötigt)

Format-String: Hier wird angegeben wie das Formatierte ausschauen soll

Split-String: Hier wird angegeben wonach jede Spalte in der TextBox aufgeteilt werden soll. Es können auch mehrere Befehle eingetragen werden, diese werden mit | getrennt.

Copy-Listener: Wenn dieser aktiviert ist, werden zukünftige Daten in der Zwischenablage jeweils in eine Zeile geschrieben (also pro STRG + C)

Search & Replace: Sucht im angegebenen Pfad nach einem Text und ersetzt ihn durch einen anderen (z.B. Alle Punkte der Dateinamen im angegebenen Pfad werden durch Leerzeichen ersetzt "Das.ist.mein.Dateiname.mkv" zu "Das ist mein Dateiname.mkv")

Insert Position: fügt bei der angegebenen Position den angegebenen Text ein

Delete Positions: löscht die Zeichen auf den angegebenen Positionen (die Positionen schreibt man daneben in das Textfeld). Positionen werden mit ";" getrennt und es kann auch von-bis angegeben werden. Also z.B. "1,5,7-10", dann werden die Zeichen auf den Positionen 1,5,7,8,9,10 gelöscht.


## Optionale TMDB-Anbindung (Entwicklungsbranch)

TMDB ergänzt die schlüsselfreie TVmaze-Anbindung um deutsche und englische Episodentitel. **TVmaze bleibt ohne Konto und ohne Schlüssel nutzbar.**

1. Einen kostenlosen persönlichen API-Key (v3) oder einen API Read Access Token bei [TMDB](https://www.themoviedb.org/settings/api) erstellen.
2. In der Episodensuche **TMDB (API-Key)** auswählen. Den Key/Token in das Passwortfeld einfügen und **Sichern** klicken. Der Wert wird lokal unter dem aktuellen Windows-Benutzer mit Windows DPAPI verschlüsselt gespeichert, nie im Repository. Mit **Löschen** lässt er sich entfernen. Auf einem anderen Windows-Konto muss er erneut eingegeben werden.
3. Suchtext eingeben und Enter drücken, Treffer wählen oder eine gültige TV-URL `https://www.themoviedb.org/tv/<ID>-...` plus **Check** verwenden.
4. Episodensprache auswählen (Deutsch bevorzugt oder Englisch). Die gewünschte Staffel, Specials (Staffel 0) oder ausdrücklich **Alle Staffeln (ohne Specials)** wählen.
5. Mit **OK** Titel abrufen. Vor der Datenübernahme zeigt ein Dialog die Anzahl deutscher/englischer sowie fehlender Titel. Englische Episodentitel ersetzen fehlende oder generische deutsche Platzhalter. Bitte die Datensätze im Editor und die Dateinamen-Reihenfolge vor dem Umbenennen überprüfen.

Die angezeigten deutschen und englischen Zahlen werden nach dem Vergleich beider Sprachfassungen bestimmt; identische Titel werden konservativ als Englisch eingeordnet. Bei fehlenden Titeln wird **kein** Titel oder absolute Anime-Episodennummer geraten. Die ersten Spalten behalten die vorhandenen Formate für einzelne Staffeln bzw. `S01E01` bei Staffelübergreifenden Listen.

**Datenquelle:** [The Movie Database (TMDB)](https://www.themoviedb.org/). This product uses the TMDB API but is not endorsed or certified by TMDB. Die TMDB-Nutzungsbedingungen und Attributionsanforderungen sind zu beachten.

## Neue TVmaze-Anbindung (Entwicklungsbranch)

Die klassische Windows-Forms-Anwendung kann Episoden jetzt über die öffentliche [TVmaze-API](https://www.tvmaze.com/api) laden – **ohne Registrierung oder API-Key**. In einer neuen Installation ist „TVmaze“ als Anbieter vorausgewählt. Bestehende Benutzer-Einstellungen bleiben erhalten; bei Bedarf „TVmaze“ im Anbieter-Menü auswählen.

1. In der Episodensuche „TVmaze“ auswählen und einen Seriennamen (Anime oder TV-Serie) eingeben; mit Enter suchen.
2. Die richtige Serie aus der Trefferliste auswählen. Optional eine `https://www.tvmaze.com/shows/<ID>/...`-URL oder numerische Serien-ID einfügen und auf **Check** klicken.
3. **Bewusst eine Staffel auswählen** oder „Alle Staffeln (ohne Specials)“ wählen.
4. Mit **OK** die Episodennummern und Titel in die vorhandene Datenbearbeitung übernehmen. Anschließend Dateinamen und Reihenfolge prüfen, bevor umbenannt wird.

Für „Alle Staffeln“ ist die erste Spalte als `S01E01`, `S01E02` usw. formatiert; für eine einzelne Staffel enthält sie die dortige Episodennummer. Die zweite Spalte enthält den Episodentitel. Specials können über „Staffel 0“ ausgewählt werden. Nicht eindeutig nummerierte oder namenlose Episoden werden **nicht automatisch zugeordnet**.

**Einschränkungen:** TVmaze liefert hauptsächlich Original-/englische Episodentitel und keine gesicherten deutschen Episodenübersetzungen. Anime mit abweichenden Staffel-, DVD- oder absoluten Episodennummern benötigen eine sorgfältige manuelle Zuordnung. Die optionale TMDB-Anbindung für deutsche Titel folgt in einem separaten Entwicklungsschritt. Die älteren HTML-basierten Anbieter sind weiterhin vorhanden, können aber wegen Website-Änderungen fehlschlagen.

**Datenquelle und Lizenz:** Episodenmetadaten von [TVmaze](https://www.tvmaze.com) – öffentliche API unter CC BY-SA (Namensnennung und Weitergabe unter gleichen Bedingungen für entsprechende weiterverteilte Daten). [API-Dokumentation und Bedingungen](https://www.tvmaze.com/api).

Details zur Architektur: [docs/METADATA_MODERNIZATION.md](docs/METADATA_MODERNIZATION.md). Ein Windows-Build und Offline-Providerchecks laufen per GitHub Actions.
