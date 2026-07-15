(function () {
    if (window.__drycar_news_init) return;
    window.__drycar_news_init = true;

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

    const endpoint = "/Home/KirsehirNews?count=12";

    const rotator = document.getElementById("newsRotator");
    const btnPrev = document.getElementById("newsPrev");
    const btnNext = document.getElementById("newsNext");

    if (!rotator) return;

    let items = [];
    let index = 0;
    let timer = null;
    const reduceMotion = window.matchMedia?.("(prefers-reduced-motion: reduce)")?.matches === true;

    function escapeHtml(str) {
        const s = (str ?? "").toString();
        return s
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#039;");
    }

    function safeHttpUrl(value) {
        try {
            const parsed = new URL((value ?? "").toString(), window.location.origin);
            return parsed.protocol === "http:" || parsed.protocol === "https:" ? parsed.href : "";
        } catch {
            return "";
        }
    }

    function renderCurrent() {
        if (!items.length) return;

        const it = items[index];

        rotator.innerHTML = `
            <div class="news-item show">
                <div class="news-dot" aria-hidden="true"></div>
                <div class="news-text">
                    <div class="news-title">
                        <a href="${escapeHtml(it.url)}" target="_blank" rel="noopener noreferrer">
                            ${escapeHtml(it.title)}
                        </a>
                    </div>
                    <div class="news-sub">Kırşehir Haber Türk’den canlı çekildi</div>
                </div>
            </div>
        `;
    }

    function next() {
        if (!items.length) return;
        index = (index + 1) % items.length;
        renderCurrent();
    }

    function prev() {
        if (!items.length) return;
        index = (index - 1 + items.length) % items.length;
        renderCurrent();
    }

    function startLoop() {
        stopLoop();
        if (!items.length || reduceMotion) return;
        timer = setInterval(next, 4500);
    }

    function stopLoop() {
        if (timer) clearInterval(timer);
        timer = null;
    }

    async function load() {
        rotator.innerHTML = `<div class="news-loading">Haberler yükleniyor...</div>`;

        try {
            const res = await fetch(endpoint, {
                cache: "no-store",
                headers: { "X-Requested-With": "XMLHttpRequest" }
            });

            if (!res.ok) throw new Error("Fetch failed: " + res.status);

            const data = await res.json();

            items = (data || [])
                .map(x => ({
                    title: x.title ?? x.Title ?? "",
                    url: x.url ?? x.Url ?? ""
                }))
                .map(x => ({
                    title: (x.title || "").trim(),
                    url: safeHttpUrl((x.url || "").trim())
                }))
                .filter(x => x.title.length > 0 && x.url.length > 0);

            if (!items.length) {
                rotator.innerHTML = `<div class="news-loading">Haber bulunamadı.</div>`;
                return;
            }

            index = 0;
            renderCurrent();
            startLoop();
        } catch (e) {
            rotator.innerHTML = `<div class="news-loading">Haberler alınamadı.</div>`;
            stopLoop();
        }
    }

    btnNext?.addEventListener("click", function () {
        next();
        startLoop();
    });

    btnPrev?.addEventListener("click", function () {
        prev();
        startLoop();
    });

    document.addEventListener("visibilitychange", function () {
        if (document.hidden) stopLoop();
        else startLoop();
    });

    btnPrev?.setAttribute("aria-label", "Önceki haberi göster");
    btnNext?.setAttribute("aria-label", "Sonraki haberi göster");
    rotator.setAttribute("aria-live", "polite");
    rotator.addEventListener("mouseenter", stopLoop);
    rotator.addEventListener("mouseleave", startLoop);
    rotator.addEventListener("focusin", stopLoop);
    rotator.addEventListener("focusout", startLoop);

    load();
})();
