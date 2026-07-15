(function () {
    if (window.__drycar_weather_init) return;
    window.__drycar_weather_init = true;

    function escapeHtml(value) {
        return (value ?? "").toString()
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#039;");
    }

    function revealSite() {
        const intro = document.getElementById("drycarIntro");
        const shell = document.getElementById("appShell");

        try {
            localStorage.setItem("drycar_home_intro_seen_v1", "1");
        } catch {
        }

        if (intro) {
            intro.classList.remove("is-on", "is-off");
            intro.setAttribute("aria-hidden", "true");
            intro.style.display = "none";
        }

        shell?.classList.remove("dc-hidden");
        document.body.classList.remove("dc-lock");
    }

    function enhanceSharedUi() {
        if (window.__drycar_shared_ui_init) return;
        window.__drycar_shared_ui_init = true;

        const intro = document.getElementById("drycarIntro");
        const clickLayer = document.getElementById("dcClickToClose");

        if (intro && intro.getAttribute("aria-hidden") !== "true") {
            clickLayer?.setAttribute("role", "button");
            clickLayer?.setAttribute("tabindex", "0");

            const skipButton = document.createElement("button");
            skipButton.type = "button";
            skipButton.className = "dc-skip-button";
            skipButton.textContent = "İntroyu geç";
            skipButton.addEventListener("click", revealSite);
            intro.appendChild(skipButton);

            if (window.matchMedia?.("(prefers-reduced-motion: reduce)")?.matches) {
                revealSite();
            } else {
                window.setTimeout(revealSite, 7000);
            }
        }

        const phone = document.getElementById("PhoneLocal");
        if (phone) {
            phone.setAttribute("aria-label", "Telefon numarası");
            phone.setAttribute("autocomplete", "tel-national");
        }

        document.getElementById("CountryCode")?.setAttribute("autocomplete", "tel-country-code");

        const autocompleteMap = {
            FirstName: "given-name",
            LastName: "family-name",
            Email: "email",
            Password: location.pathname.toLowerCase().includes("register") ? "new-password" : "current-password"
        };

        Object.entries(autocompleteMap).forEach(([id, value]) => {
            document.getElementById(id)?.setAttribute("autocomplete", value);
        });

        const capturedImage = document.getElementById("captured-image");
        if (capturedImage?.getAttribute("src") === "") capturedImage.removeAttribute("src");

        document.getElementById("cookie-analytics")?.setAttribute("aria-label", "Analitik çerezlere izin ver");
        document.getElementById("cookie-marketing")?.setAttribute("aria-label", "Pazarlama çerezlerine izin ver");

        document.querySelectorAll(".social-fixed a").forEach(link => {
            const imageAlt = link.querySelector("img")?.alt;
            if (imageAlt) link.setAttribute("aria-label", imageAlt + " sayfasını aç");
        });
    }

    enhanceSharedUi();

    const box = document.getElementById("weatherBox");
    if (!box) return;

    // Kırşehir koordinat
    const LAT = 39.1461;
    const LON = 34.1606;

    const serverEndpoint = "/Home/KirsehirWeather";

    function finiteNumber(x) {
        if (x === null || x === undefined || typeof x === "boolean") return null;
        if (typeof x === "string" && x.trim() === "") return null;

        const n = Number(x);
        return Number.isFinite(n) ? n : null;
    }

    function num(x) {
        return finiteNumber(x) ?? 0;
    }

    function mapWeatherCode(code) {
        switch (Number(code)) {
            case 0: return "Açık";
            case 1:
            case 2: return "Az Bulutlu";
            case 3: return "Bulutlu";
            case 45:
            case 48: return "Sisli";
            case 51:
            case 53:
            case 55: return "Çisenti";
            case 61:
            case 63:
            case 65: return "Yağmurlu";
            case 71:
            case 73:
            case 75: return "Karlı";
            case 80:
            case 81:
            case 82: return "Sağanak";
            case 95:
            case 96:
            case 99: return "Fırtınalı";
            default: return "Parçalı Bulutlu";
        }
    }

    function render(w) {
        if (!w) {
            box.innerHTML = `<div class="news-loading">Hava durumu alınamadı.</div>`;
            return;
        }

        const city = escapeHtml(w.city ?? w.City ?? "Kırşehir");
        const summary = escapeHtml(w.summary ?? w.Summary ?? "");

        const temp = Math.round(num(w.temperatureC ?? w.TemperatureC));
        const feels = Math.round(num(w.feelsLikeC ?? w.FeelsLikeC));
        const wind = Math.round(num(w.windKmh ?? w.WindKmh));
        const hum = Math.round(num(w.humidity ?? w.Humidity));

        box.innerHTML = `
          <div class="weather-wrap">
            <div class="weather-top">
              <div>
                <div class="weather-city">${city}</div>
                <div class="weather-summary">${summary}</div>
              </div>
              <div class="weather-temp">${temp}°</div>
            </div>

            <div class="weather-grid">
              <div class="weather-pill">
                <div class="weather-pill-k">Hissedilen</div>
                <div class="weather-pill-v">${feels}°C</div>
              </div>
              <div class="weather-pill">
                <div class="weather-pill-k">Nem</div>
                <div class="weather-pill-v">%${hum}</div>
              </div>
              <div class="weather-pill">
                <div class="weather-pill-k">Rüzgar</div>
                <div class="weather-pill-v">${wind} km/s</div>
              </div>
            </div>
          </div>
        `;
    }

    async function fetchFromServer() {
        try {
            const res = await fetch(serverEndpoint, { cache: "no-store" });
            if (!res.ok) return null;
            const data = await res.json();
            // server "null" döndürüyorsa data null olur
            if (!data) return null;
            return data;
        } catch {
            return null;
        }
    }

    async function fetchDirectOpenMeteo() {
        try {
            const tz = encodeURIComponent("Europe/Istanbul");
            const url =
                `https://api.open-meteo.com/v1/forecast` +
                `?latitude=${LAT}&longitude=${LON}` +
                `&current=temperature_2m,wind_speed_10m,apparent_temperature,weather_code,relative_humidity_2m` +
                `&timezone=${tz}`;

            const res = await fetch(url, { cache: "no-store" });
            if (!res.ok) return null;
            const j = await res.json();

            const c = j && j.current ? j.current : null;
            if (!c) return null;

            const temp = finiteNumber(c.temperature_2m);
            if (temp === null) return null;

            return {
                city: "Kırşehir",
                temperatureC: temp,
                feelsLikeC: num(c.apparent_temperature),
                windKmh: num(c.wind_speed_10m),
                humidity: Math.round(num(c.relative_humidity_2m)),
                summary: mapWeatherCode(c.weather_code)
            };
        } catch {
            return null;
        }
    }

    async function load() {
        box.innerHTML = `<div class="news-loading">Hava durumu yükleniyor...</div>`;

        // 1) Önce backend dene
        let w = await fetchFromServer();

        // 2) Backend null/bozuksa direkt Open-Meteo
        if (!w) {
            w = await fetchDirectOpenMeteo();
        }

        render(w);
    }

    load();
    setInterval(load, 10 * 60 * 1000);
})();
