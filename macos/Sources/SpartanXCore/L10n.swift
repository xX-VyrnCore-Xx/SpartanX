import Foundation

/// Tiny runtime-switchable localisation (English + Italian), mirroring the Windows app.
public enum L10n {
    nonisolated(unsafe) public static var language: String = resolve(nil)

    public static func resolve(_ preferred: String?) -> String {
        let tag = (preferred?.isEmpty == false && preferred != "system")
            ? preferred!
            : (Locale.preferredLanguages.first ?? "en")
        return tag.lowercased().hasPrefix("it") ? "it" : "en"
    }

    public static func get(_ key: String) -> String {
        tables[language]?[key] ?? tables["en"]?[key] ?? key
    }

    public static func format(_ key: String, _ args: CVarArg...) -> String {
        String(format: get(key), arguments: args)
    }

    public static let greetings = [
        "hello", "ciao", "bonjour", "hola", "hallo", "olá", "こんにちは", "привет", "你好", "안녕하세요", "merhaba", "hej",
    ]

    public static let tables: [String: [String: String]] = ["en": en, "it": it]

    static let en: [String: String] = [
        "menu.settings": "Settings…",
        "menu.welcome": "Show Welcome…",
        "menu.updates": "Check for Updates…",
        "menu.quit": "Quit SpartanX",
        "menu.island": "Dynamic Island",

        "island.timer": "Timer",
        "island.timerDone": "Timer finished",
        "island.quickTimer": "Quick timer",
        "island.charging": "Charging",
        "island.pluggedIn": "Plugged in",
        "island.lowBattery": "Low battery",
        "island.notPlaying": "Nothing playing",

        "welcome.getStarted": "Get started",
        "welcome.continue": "Continue",
        "welcome.back": "Back",
        "welcome.start": "Start using SpartanX",
        "welcome.island.title": "Your Dynamic Island",
        "welcome.island.subtitle": "Live activities around the notch. Choose what it shows.",
        "welcome.privacy.title": "Permissions & startup",
        "welcome.privacy.subtitle": "SpartanX runs entirely on your Mac.",
        "welcome.privacy.automation": "To show Now Playing, macOS will ask once to let SpartanX control Music and Spotify.",
        "welcome.privacy.noTelemetry": "No telemetry, no account. The network is only used to check GitHub for updates and, if enabled, the weather.",
        "welcome.done.title": "You're all set",
        "welcome.done.subtitle": "Hover the notch to peek, click it to open. SpartanX lives in the menu bar.",

        "settings.general": "General",
        "settings.island": "Dynamic Island",
        "settings.animations": "Animations",
        "settings.about": "About",
        "settings.language": "Language",
        "settings.language.system": "Same as macOS",
        "settings.launchAtLogin": "Open at login",
        "settings.launchAtLogin.failed": "macOS refused: move SpartanX to Applications and try again.",
        "settings.updates": "Check for updates automatically",
        "settings.prereleases": "Include beta versions",
        "settings.checkNow": "Check Now",
        "settings.upToDate": "SpartanX is up to date.",
        "settings.updateAvailable": "Version %@ is available.",
        "settings.download": "Download",
        "settings.checking": "Checking…",
        "settings.runWelcome": "Run Setup Assistant…",
        "settings.restartForLanguage": "Some text updates after restarting SpartanX.",
        "settings.interaction": "Interaction",
        "settings.interaction.hoverToPeek": "Hover to peek, click to open",
        "settings.interaction.clickOnly": "Click only",
        "settings.autoExpand": "Expand for new activity",
        "settings.autoCollapse": "Collapse after",
        "settings.allScreens": "Show on every display",
        "settings.modules": "Modules",
        "settings.media": "Now Playing (Music, Spotify)",
        "settings.timer": "Timer",
        "settings.battery": "Battery & charging",
        "settings.weather": "Weather",
        "settings.city": "City",
        "settings.cityPlaceholder": "e.g. Rome",
        "settings.fahrenheit": "Use Fahrenheit",
        "settings.motion": "Motion style",
        "settings.motion.smooth": "Smooth",
        "settings.motion.snappy": "Snappy",
        "settings.motion.bouncy": "Bouncy",
        "settings.reduceMotion": "Reduce motion",
        "settings.tapToPreview": "Click to preview",
        "settings.version": "Version %@",
        "settings.tagline": "A Dynamic Island for your Mac.",
        "settings.source": "Source code on GitHub",

        "weather.clear": "Clear", "weather.partlyCloudy": "Partly cloudy", "weather.overcast": "Overcast",
        "weather.fog": "Fog", "weather.drizzle": "Drizzle", "weather.rain": "Rain", "weather.snow": "Snow",
        "weather.thunderstorm": "Thunderstorm", "weather.unknown": "—",
    ]

    static let it: [String: String] = [
        "menu.settings": "Impostazioni…",
        "menu.welcome": "Mostra benvenuto…",
        "menu.updates": "Controlla aggiornamenti…",
        "menu.quit": "Esci da SpartanX",
        "menu.island": "Dynamic Island",

        "island.timer": "Timer",
        "island.timerDone": "Timer terminato",
        "island.quickTimer": "Timer rapido",
        "island.charging": "In carica",
        "island.pluggedIn": "Collegato",
        "island.lowBattery": "Batteria scarica",
        "island.notPlaying": "Nessuna riproduzione",

        "welcome.getStarted": "Inizia",
        "welcome.continue": "Continua",
        "welcome.back": "Indietro",
        "welcome.start": "Inizia a usare SpartanX",
        "welcome.island.title": "La tua Dynamic Island",
        "welcome.island.subtitle": "Attività in tempo reale attorno al notch. Scegli cosa mostrare.",
        "welcome.privacy.title": "Permessi e avvio",
        "welcome.privacy.subtitle": "SpartanX funziona interamente sul tuo Mac.",
        "welcome.privacy.automation": "Per mostrare la musica in riproduzione, macOS chiederà una volta il permesso di controllare Musica e Spotify.",
        "welcome.privacy.noTelemetry": "Nessuna telemetria, nessun account. La rete serve solo a cercare aggiornamenti su GitHub e, se attivo, il meteo.",
        "welcome.done.title": "Tutto pronto",
        "welcome.done.subtitle": "Passa sopra il notch per l'anteprima, cliccalo per aprirlo. SpartanX vive nella barra dei menu.",

        "settings.general": "Generali",
        "settings.island": "Dynamic Island",
        "settings.animations": "Animazioni",
        "settings.about": "Informazioni",
        "settings.language": "Lingua",
        "settings.language.system": "Come macOS",
        "settings.launchAtLogin": "Apri all'accesso",
        "settings.launchAtLogin.failed": "macOS ha rifiutato: sposta SpartanX in Applicazioni e riprova.",
        "settings.updates": "Cerca aggiornamenti automaticamente",
        "settings.prereleases": "Includi versioni beta",
        "settings.checkNow": "Controlla ora",
        "settings.upToDate": "SpartanX è aggiornato.",
        "settings.updateAvailable": "È disponibile la versione %@.",
        "settings.download": "Scarica",
        "settings.checking": "Controllo in corso…",
        "settings.runWelcome": "Esegui Assistente configurazione…",
        "settings.restartForLanguage": "Alcuni testi si aggiornano dopo il riavvio di SpartanX.",
        "settings.interaction": "Interazione",
        "settings.interaction.hoverToPeek": "Passa sopra per l'anteprima, clic per aprire",
        "settings.interaction.clickOnly": "Solo clic",
        "settings.autoExpand": "Espandi per nuove attività",
        "settings.autoCollapse": "Richiudi dopo",
        "settings.allScreens": "Mostra su tutti i monitor",
        "settings.modules": "Moduli",
        "settings.media": "In riproduzione (Musica, Spotify)",
        "settings.timer": "Timer",
        "settings.battery": "Batteria e ricarica",
        "settings.weather": "Meteo",
        "settings.city": "Città",
        "settings.cityPlaceholder": "es. Roma",
        "settings.fahrenheit": "Usa Fahrenheit",
        "settings.motion": "Stile di movimento",
        "settings.motion.smooth": "Fluido",
        "settings.motion.snappy": "Scattante",
        "settings.motion.bouncy": "Elastico",
        "settings.reduceMotion": "Riduci movimento",
        "settings.tapToPreview": "Clicca per l'anteprima",
        "settings.version": "Versione %@",
        "settings.tagline": "Una Dynamic Island per il tuo Mac.",
        "settings.source": "Codice sorgente su GitHub",

        "weather.clear": "Sereno", "weather.partlyCloudy": "Parzialmente nuvoloso", "weather.overcast": "Coperto",
        "weather.fog": "Nebbia", "weather.drizzle": "Pioviggine", "weather.rain": "Pioggia", "weather.snow": "Neve",
        "weather.thunderstorm": "Temporale", "weather.unknown": "—",
    ]
}
