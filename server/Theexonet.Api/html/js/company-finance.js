import { formatRaxHtml } from "./currency.js";

let activeFinanceTab = "overview";
/** @type {object | null} */
let financeCtx = null;

export function wireCompanyFinance(ctx) {
  financeCtx = ctx;
  const { els } = ctx;
  const tabs = els.financeModal?.querySelectorAll("[data-finance-tab]");
  tabs?.forEach((button) => {
    button.addEventListener("click", () => {
      activeFinanceTab = button.dataset.financeTab ?? "overview";
      renderCompanyFinancePanel();
    });
  });

  els.financeHireBtn?.addEventListener("click", () => {
    runCrewAction(
      () => ctx.api.hireWorker(ctx.api.mineId),
      ctx.t("finance.crew.hireConfirm"),
    );
  });

  els.financeRenewRightsBtn?.addEventListener("click", () => {
    runCrewAction(() => ctx.api.renewMiningRights(ctx.api.mineId));
  });
}

export function renderCompanyFinancePanel() {
  if (!financeCtx) {
    return;
  }

  const { els, state, t, formatRaxLabelLine, formatRunway } = financeCtx;
  const finances = state.finances;
  if (!finances) {
    return;
  }

  const tabs = els.financeModal?.querySelectorAll("[data-finance-tab]");
  const panels = els.financeModal?.querySelectorAll("[data-finance-panel]");
  tabs?.forEach((button) => {
    button.classList.toggle("active", button.dataset.financeTab === activeFinanceTab);
  });
  panels?.forEach((panel) => {
    panel.hidden = panel.dataset.financePanel !== activeFinanceTab;
  });

  renderOverviewTab(finances);
  renderCrewTab(finances);
  renderObligationsTab(finances);
  renderActivityTab(finances);

  function renderOverviewTab(f) {
    els.financeSummary.innerHTML = [
      formatRaxLabelLine(t("finance.operatingBalance"), f.credits),
      formatRaxLabelLine(t("finance.reserveBalance"), f.reserveBalance ?? 0),
      formatRaxLabelLine(t("finance.jobSalary"), f.dailyJobSalary ?? 0),
      formatRaxLabelLine(t("finance.payroll"), f.dailyPayroll),
      formatRaxLabelLine(t("finance.companyObligations"), f.dailyCompanyObligations ?? 0),
      formatRaxLabelLine(t("finance.totalReserveBurn"), f.dailyTotalReserveBurn ?? 0),
      formatRaxLabelLine(t("finance.supplyCost"), f.dailySupplyCost),
      formatRaxLabelLine(t("finance.estIncome"), f.estimatedDailyIncome),
      `${t("finance.runway")}: ${formatRunway(f.runwayDays)} ${t("finance.runwayDays")}`,
      f.isSoftlocked ? `<strong class='danger'>${t("finance.softlocked")}</strong>` : "",
    ]
      .filter(Boolean)
      .join("<br>");

    els.emergencyBtn.hidden = !f.canEmergencyBuyback;
    els.financeTransactions.innerHTML = "";
    for (const tx of (f.recentTransactions ?? []).slice(0, 8)) {
      els.financeTransactions.appendChild(buildTxLine(tx));
    }
  }

  function renderCrewTab(f) {
    const workers = f.workers ?? [];
    els.financeCrewSummary.innerHTML = [
      formatRaxLabelLine(t("finance.payroll"), f.dailyPayroll),
      `${t("finance.crew.count")}: ${workers.length}`,
    ].join("<br>");

    els.financeCrewList.innerHTML = "";
    for (const worker of workers) {
      const row = document.createElement("div");
      row.className = "finance-crew-row";
      const zoneLabel = worker.assignedZoneId ? t("finance.crew.assigned") : t("finance.crew.unassigned");
      row.innerHTML = [
        `<strong>${escapeHtml(worker.name)}</strong>`,
        `${t("finance.crew.skill")}: ${worker.skill}`,
        `${t("finance.crew.salary")}: ${formatRaxHtml(worker.salary)}`,
        zoneLabel,
      ].join(" · ");

      const actions = document.createElement("div");
      actions.className = "finance-crew-actions";

      const raiseBtn = document.createElement("button");
      raiseBtn.type = "button";
      raiseBtn.className = "btn ghost";
      raiseBtn.textContent = t("finance.crew.raise");
      raiseBtn.addEventListener("click", () => {
        const input = window.prompt(t("finance.crew.raisePrompt", { current: String(worker.salary) }));
        const newSalary = Number(input);
        if (!Number.isFinite(newSalary) || newSalary <= worker.salary) {
          return;
        }
        runCrewAction(() => financeCtx.api.raiseWorker(financeCtx.api.mineId, worker.id, newSalary));
      });

      const layoffBtn = document.createElement("button");
      layoffBtn.type = "button";
      layoffBtn.className = "btn ghost";
      layoffBtn.textContent = t("finance.crew.layoff");
      layoffBtn.addEventListener("click", () => {
        if (!window.confirm(t("finance.crew.layoffConfirm", { name: worker.name }))) {
          return;
        }
        runCrewAction(() => financeCtx.api.layoffWorker(financeCtx.api.mineId, worker.id));
      });

      const fireBtn = document.createElement("button");
      fireBtn.type = "button";
      fireBtn.className = "btn danger";
      fireBtn.textContent = t("finance.crew.fire");
      fireBtn.addEventListener("click", () => {
        if (!window.confirm(t("finance.crew.fireConfirm", { name: worker.name }))) {
          return;
        }
        runCrewAction(() => financeCtx.api.fireWorker(financeCtx.api.mineId, worker.id));
      });

      actions.append(raiseBtn, layoffBtn, fireBtn);
      row.appendChild(actions);
      els.financeCrewList.appendChild(row);
    }
  }

  function renderObligationsTab(f) {
    const obligations = f.dailyObligations;
    const rights = f.miningRights;
    if (!obligations) {
      return;
    }

    els.financeObligationsSummary.innerHTML = [
      formatRaxLabelLine(t("finance.obligations.tax"), obligations.companyTax),
      formatRaxLabelLine(t("finance.obligations.health"), obligations.healthInsurance),
      formatRaxLabelLine(t("finance.obligations.job"), obligations.jobInsurance),
      formatRaxLabelLine(t("finance.obligations.beltFee"), obligations.beltFee),
      formatRaxLabelLine(t("finance.obligations.miningRights"), obligations.miningRights),
      formatRaxLabelLine(t("finance.obligations.total"), obligations.total),
    ].join("<br>");

    if (rights) {
      els.financeMiningRightsStatus.textContent = rights.isExpired
        ? t("finance.obligations.rightsExpired")
        : t("finance.obligations.rightsValid", { day: String(rights.paidThroughDay) });
      els.financeRenewRightsBtn.hidden = false;
    }
  }

  function renderActivityTab(f) {
    els.financeCompanyActivity.innerHTML = "";
    for (const tx of (f.companyReserveActivity ?? []).slice(0, 16)) {
      els.financeCompanyActivity.appendChild(buildReserveTxLine(tx));
    }
  }
}

async function runCrewAction(action, confirmMessage) {
  if (!financeCtx) {
    return;
  }

  if (confirmMessage && !window.confirm(confirmMessage)) {
    return;
  }

  const { refreshAll, showStatus, t } = financeCtx;
  showStatus(t("finance.crew.working"));
  try {
    await action();
    await refreshAll();
    renderCompanyFinancePanel();
    showStatus("");
  } catch (error) {
    showStatus(error.message, true);
  }
}

function buildTxLine(tx) {
  const line = document.createElement("div");
  const sign = Number(tx.amount) >= 0 ? "+" : "";
  line.append(`Day ${tx.gameDay}: `);
  const amountWrap = document.createElement("span");
  amountWrap.innerHTML = `${sign}${formatRaxHtml(tx.amount)}`;
  line.append(amountWrap, ` — ${tx.description ?? ""}`);
  return line;
}

function buildReserveTxLine(tx) {
  const line = document.createElement("div");
  const sign = Number(tx.amount) >= 0 ? "+" : "";
  line.append(`Day ${tx.gameDay}: `);
  const amountWrap = document.createElement("span");
  amountWrap.innerHTML = `${sign}${formatRaxHtml(tx.amount)}`;
  line.append(amountWrap, ` — ${tx.description ?? ""}`);
  return line;
}

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}
