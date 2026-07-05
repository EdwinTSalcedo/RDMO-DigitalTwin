const navLinks = [...document.querySelectorAll(".nav a")];
const sections = navLinks
  .map((link) => document.querySelector(link.getAttribute("href")))
  .filter(Boolean);

const observer = new IntersectionObserver(
  (entries) => {
    for (const entry of entries) {
      if (!entry.isIntersecting) continue;
      navLinks.forEach((link) => {
        link.classList.toggle("active", link.getAttribute("href") === `#${entry.target.id}`);
      });
    }
  },
  { rootMargin: "-30% 0px -60% 0px", threshold: 0.01 },
);

sections.forEach((section) => observer.observe(section));

document.querySelectorAll("[data-tabs]").forEach((tabs) => {
  const buttons = [...tabs.querySelectorAll("[data-panel]")];
  const panels = [...tabs.querySelectorAll(".tab-panel")];

  buttons.forEach((button) => {
    button.addEventListener("click", () => {
      const target = button.dataset.panel;
      buttons.forEach((item) => item.classList.toggle("active", item === button));
      panels.forEach((panel) => panel.classList.toggle("active", panel.id === `panel-${target}`));
    });
  });
});

document.querySelectorAll("[data-copy]").forEach((button) => {
  button.addEventListener("click", async () => {
    const target = document.querySelector(button.dataset.copy);
    const text = target?.innerText.trim();
    if (!text) return;

    try {
      await navigator.clipboard.writeText(text);
      const original = button.textContent;
      button.textContent = "Copied";
      window.setTimeout(() => {
        button.textContent = original;
      }, 1400);
    } catch {
      button.textContent = "Select BibTeX manually";
    }
  });
});

document.querySelectorAll("[data-video-label]").forEach((button) => {
  button.addEventListener("click", () => {
    const label = button.dataset.videoLabel;
    const original = button.querySelector(".video-label")?.textContent;
    const labelElement = button.querySelector(".video-label");
    if (!labelElement) return;

    labelElement.textContent = `${label}: video placeholder`;
    window.setTimeout(() => {
      labelElement.textContent = original;
    }, 1500);
  });
});
