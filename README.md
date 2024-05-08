# S7CommPlusProxy
Mitm proxy für ISO on tcp Verbindungen.
## Entwicklungsstand
Dies ist aktuell ein Entwicklungsstand und nicht für den Produktiveinsatz vorgesehen.
Dieser proxy soll nur dazu dienen an die ausgehandelten Secrets der TLS Verbindung zu kommen um dies besser in Wireshark analysieren zu können.

## How to use
Als erstes müssen diese zwei Zertifikat Dateien im Arbeitsverzeichnis der Anwendung erstellt werden `test.crt.pem` und `test.key.pem`.

Das Zertifikat kann man sich am leichtesten mit dem Zertifikatsmanager im TIA Portal generieren. Das generierte Zertifikat anschließend als `.p12` Archiv mit privatem Schlüssel exportieren und mit Hilfe von OpenSSL die `.pem` Dateien erzeugen:

`openssl pkcs12 -in "test_X.509 Certificate_3.p12" -out test.crt.pem -clcerts -nokeys`

`openssl pkcs12 -in "test_X.509 Certificate_3.p12" -out test.key.pem -nocerts -nodes`

Achtung! Wenn auf dem PC ein Siemens Produkt installiert ist, läuft mit hoher Wahrscheinlichkeit der `S7DOS Help Service`. Dieser blockiert den benötigten Port 102. 
Um dieses Problem zu umgehen stoppt die Anwendung den Service kurz und startet ihn wieder, nachdem der Server läuft. Bitte in diesem Fall immer die IP eines Interfaces angeben auf das der Server gebunden werden soll!

## Lizenz
Soweit nicht anders vermerkt, gilt für alle Quellcodes die GNU Lesser General Public License,
Version 3 oder später. 

Dieses Projekt basiert zu großen Teilen auf der hervorragenden Arbeit von **Thomas Wiens** - [thomas-v2](https://github.com/thomas-v2) und seinem [s7CommPlusDriver](https://github.com/thomas-v2/S7CommPlusDriver)

## Authors
* **IchEben** - *Initial work* - [Ich-Eben](https://github.com/Ich-Eben)
* **Thomas Wiens** - *Initial work on the s7CommPlusDrive* - [thomas-v2](https://github.com/thomas-v2)
