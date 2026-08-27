(function () {
  function calculateCountdown(startAt, endAt, now = Date.now(), labels = {}) {
    const start = startAt ? Date.parse(startAt) : null;
    const end = Date.parse(endAt);
    if (!Number.isFinite(end) || (start !== null && !Number.isFinite(start))) return null;

    const ended = now >= end;
    const upcoming = start !== null && now < start;
    const target = upcoming ? start : end;
    const remaining = ended ? 0 : Math.max(0, Math.ceil((target - now) / 1000));
    const days = Math.floor(remaining / 86400);
    const hours = Math.floor((remaining % 86400) / 3600);
    const minutes = Math.floor((remaining % 3600) / 60);
    const seconds = remaining % 60;
    const progress = ended || start === null || end <= start
      ? ended ? 100 : 0
      : upcoming ? 0 : Math.min(100, Math.max(0, ((now - start) / (end - start)) * 100));
    const label = ended ? labels.ended ?? "" : upcoming ? labels.upcoming ?? "" : labels.live ?? "";
    const dayLabel = labels.days ?? "";
    const hourLabel = labels.hours ?? "";
    const minuteLabel = labels.minutes ?? "";
    const secondLabel = labels.seconds ?? "";
    const state = ended ? "ended" : upcoming ? "upcoming" : "live";
    const pad = value => String(value).padStart(2, "0");

    return {
      state,
      label,
      days: pad(days),
      hours: pad(hours),
      minutes: pad(minutes),
      seconds: pad(seconds),
      progress,
      ariaLabel: ended ? label : `${label} ${days} ${dayLabel}, ${hours} ${hourLabel}, ${minutes} ${minuteLabel}, ${seconds} ${secondLabel}`
    };
  }

  function initialize(root = document) {
    root.querySelectorAll("[data-public-countdown]").forEach(element => {
      const values = Object.fromEntries(["days", "hours", "minutes", "seconds"].map(name => [name, element.querySelector(`[data-countdown-value="${name}"]`)]));
      const label = element.querySelector("[data-countdown-label]");
      const progress = element.querySelector("[data-countdown-progress]");
      let timer;
      const update = () => {
        const countdown = calculateCountdown(element.dataset.countdownStart, element.dataset.countdownEnd, Date.now(), {
          live: element.dataset.countdownLiveLabel,
          upcoming: element.dataset.countdownUpcomingLabel,
          ended: element.dataset.countdownEndedLabel,
          days: element.dataset.countdownDaysLabel,
          hours: element.dataset.countdownHoursLabel,
          minutes: element.dataset.countdownMinutesLabel,
          seconds: element.dataset.countdownSecondsLabel
        });
        if (!countdown) return null;
        label.textContent = countdown.label;
        Object.entries(values).forEach(([name, value]) => { value.textContent = countdown[name]; });
        progress.style.width = `${countdown.progress}%`;
        element.setAttribute("aria-label", countdown.ariaLabel);
        if (countdown.state === "ended" && timer) window.clearInterval(timer);
        return countdown;
      };
      const initial = update();
      if (initial && initial.state !== "ended") timer = window.setInterval(update, 1000);
    });
  }

  const api = { calculateCountdown, initialize };
  if (typeof window !== "undefined") {
    window.publicCountdown = api;
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", () => initialize());
    else initialize();
  }
  if (typeof module !== "undefined" && module.exports) module.exports = api;
})();
