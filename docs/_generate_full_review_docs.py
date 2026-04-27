# -*- coding: utf-8 -*-
"""Generate long-form senior review HTML documents with embedded source code."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def esc(s: str) -> str:
    return (
        s.replace("&", "&amp;")
        .replace("<", "&lt;")
        .replace(">", "&gt;")
    )


def read_rel(rel: str) -> str:
    return (ROOT / rel).read_text(encoding="utf-8")


def pre_block(title: str, path: str, content: str) -> str:
    return (
        f'<h3 id="{html_anchor(path)}">{esc(title)}</h3>'
        f'<p class="path"><code>{esc(path)}</code> — verbatim</p>'
        f"<pre><code>{esc(content)}</code></pre>\n"
    )


def html_anchor(path: str) -> str:
    return path.replace("/", "-").replace("\\", "-").replace(".", "-")


# --- Document 1: Code Review ---
css = """
:root { --bg:#fafafa; --text:#111; --muted:#444; --border:#ccc; --code:#0d1117; --code-fg:#e6edf3; }
* { box-sizing: border-box; }
body { font-family: Georgia, "Times New Roman", serif; line-height: 1.5; color: var(--text); background: var(--bg); margin: 0; padding: 0; }
.doc { max-width: 52rem; margin: 0 auto; padding: 2rem 1.25rem 6rem; font-size: 11.5pt; }
h1 { font-family: system-ui, sans-serif; font-size: 1.85rem; border-bottom: 3px double #333; padding-bottom: 0.4rem; }
h2 { font-family: system-ui, sans-serif; font-size: 1.35rem; margin-top: 2.25rem; page-break-after: avoid; }
h3 { font-family: system-ui, sans-serif; font-size: 1.05rem; margin-top: 1.4rem; }
p.path { font-size: 0.9rem; color: var(--muted); margin: 0.2rem 0 0.5rem; }
nav.toc { background: #fff; border: 1px solid var(--border); padding: 1rem 1.25rem; margin: 1.5rem 0; columns: 2; column-gap: 2rem; }
nav.toc ul { margin: 0.4rem 0 0; padding-left: 1.2rem; }
nav.toc li { margin: 0.15rem 0; break-inside: avoid; }
pre { background: var(--code); color: var(--code-fg); padding: 0.85rem 1rem; overflow-x: auto; font-size: 8.5pt; line-height: 1.35;
  border: 1px solid #30363d; border-radius: 6px; page-break-inside: auto; white-space: pre; font-family: Consolas, monospace; }
code { font-family: Consolas, monospace; font-size: 0.92em; }
.meta { color: var(--muted); font-size: 0.95rem; }
.note { border-left: 4px solid #0969da; padding: 0.6rem 1rem; background: #f6f8fa; margin: 1rem 0; }
.warn { border-left: 4px solid #9a6700; padding: 0.6rem 1rem; background: #fff8c5; margin: 1rem 0; }
table { border-collapse: collapse; width: 100%; font-size: 0.95rem; margin: 1rem 0; }
th, td { border: 1px solid var(--border); padding: 0.4rem 0.55rem; text-align: left; vertical-align: top; }
th { background: #eaeaea; }
@media print {
  body { background: #fff; }
  .doc { max-width: none; font-size: 10pt; padding: 0.5in; }
  pre { font-size: 7.5pt; }
  nav.toc { columns: 1; page-break-after: always; }
}
"""

narrative_intro = """
<div class="doc">
<header>
<h1>Elite Restaurant — Full Technical Code Review</h1>
<p class="meta"><strong>Generated:</strong> repository snapshot · <strong>Purpose:</strong> senior engineer review with <em>complete embedded implementations</em> (Core, Pro services, full ViewModel, API, SeedRunner, all tests, AppDbContext) · <strong>Print:</strong> Save as PDF — typically <strong>60–120+ pages</strong> at default margins (code-heavy).</p>
</header>

<nav class="toc">
<strong>Table of contents</strong>
<ul>
<li><a href="#summary">1. Executive summary</a></li>
<li><a href="#scope">2. Scope and methodology</a></li>
<li><a href="#map">3. Solution map</a></li>
<li><a href="#pipeline">4. Order pipeline (logic narrative)</a></li>
<li><a href="#totals-math">5. Totals &amp; discount mathematics</a></li>
<li><a href="#core-listings">6. Core layer — full source listings</a></li>
<li><a href="#pro-listings">7. Desktop services &amp; ViewModel — full listings</a></li>
<li><a href="#api-listings">8. API &amp; configuration — full listings</a></li>
<li><a href="#safety">9. Operational safety (SeedRunner)</a></li>
<li><a href="#tests">10. Automated tests (full listings)</a></li>
<li><a href="#risks">11. Residual risks &amp; recommendations</a></li>
</ul>
</nav>

<h2 id="summary">1. Executive summary</h2>
<p>This document is a <strong>code-first review</strong>: after a concise assessment, it embeds the <strong>actual source code</strong> that implements the remediated areas (create-order decomposition, shared order helpers, PostgreSQL API hosting, tablet auth, configurable pricing, destructive seed guard, WPF database dialog, and selected tests). A companion file, <code>Architecture-and-Refactor-EliteRestaurant-FULL.html</code>, covers system structure, PostgreSQL, API, and front-end architecture with the same level of source detail.</p>
<p><strong>Bottom line:</strong> The codebase uses a <strong>shared Core</strong> library for EF Core + domain rules, a <strong>WPF</strong> admin client, and an <strong>ASP.NET Core</strong> API with static Server Portal. Recent refactors moved create-order persistence into dedicated services and pushed pure totals logic into <code>OrderTotalsCalculator</code> (Core). Automated tests exist but do not yet blanket all financial paths.</p>

<h2 id="scope">2. Scope and methodology</h2>
<p><strong>In scope:</strong> Files directly touched by the audit-style remediation and their immediate dependencies (totals, discount parsing, submission helper, reconciler query extensions, tests). <strong>Out of scope:</strong> Line-by-line review of every report screen and every migration historical artifact.</p>
<p><strong>Method:</strong> Read the repository; reproduce authoritative listings via this generator script (<code>docs/_generate_full_review_docs.py</code>) so the PDF cannot drift from Git without regenerating.</p>

<h2 id="map">3. Solution map</h2>
<table>
<thead><tr><th>Project</th><th>Role</th></tr></thead>
<tbody>
<tr><td><code>EliteRestaurant.Core</code></td><td>EF models, migrations, domain helpers, <code>AppDbContext</code></td></tr>
<tr><td><code>EliteRestaurantPro</code></td><td>WPF UI, ViewModels, <code>Services/</code> for create-order</td></tr>
<tr><td><code>EliteRestaurant.Api</code></td><td>REST API, <code>wwwroot</code> Server Portal, Swagger</td></tr>
<tr><td><code>EliteRestaurant.Tests</code></td><td>xUnit, EF InMemory for reconciliation</td></tr>
<tr><td><code>SeedRunner</code></td><td>Destructive reseed with confirmation / <code>--force</code></td></tr>
</tbody>
</table>

<h2 id="pipeline">4. Order pipeline (logic narrative)</h2>
<p><strong>Phase 1 — <code>LoadPhase1</code>:</strong> For non-delivery orders, load the table with assigned server; enforce tablet server assignment when <code>AppSession.IsServerTablet</code>; query the latest <em>open check</em> via <code>WhereOpenCheckForTable</code> (translatable EF predicate for Pending cashier / Waiting / In Kitchen / Ready / Served).</p>
<p><strong>Append path — <code>AppendToExisting</code>:</strong> Add line items with <code>OrderSubmissionHelper.ResolveAssignee</code> (drinks → barman, else chef); merge notes; optionally update discount; <code>SyncPaymentFields</code>; <code>ApplyReservationLink</code>; if not pending cashier, run <code>OrderInventoryDeduction.TryApplyForAdditionalItems</code>; <code>SaveChanges</code>; <code>DataReconciler.ReconcileTableStatusesWithOrders</code>; commit transaction.</p>
<p><strong>New order — <code>SaveNew</code>:</strong> Build <code>OrderRecord</code> with status <code>PendingCashier</code> for tablet staff flow or selected status otherwise; inventory check for non-tablet flow; link reservation; mark table occupied; reconcile.</p>

<h2 id="totals-math">5. Totals &amp; discount mathematics</h2>
<p>Subtotal after discount: <code>taxable = round(lineItemsSubtotal - discountApplied)</code> per <code>RoundingSubtotal</code> setting. Tax and service apply to taxable amount using <code>TaxPercent</code> and <code>ServicePercent</code> from <code>SettingsManager.Load().CurrencyPricing</code> with defaults 7% / 10% when unset. Grand total rounds via <code>RoundingGrandTotal</code>. <code>OrderTotalsCalculator</code> composes <code>OrderDiscountParser</code>, <code>OrderTotalsHelper</code>, and <code>OrderPrepTimeEstimator</code>.</p>

<h2 id="core-listings">6. Core layer — full source listings</h2>
"""

# Build body with all core files
core_files = [
    ("OrderDiscountParser.cs", "EliteRestaurant.Core/Utils/OrderDiscountParser.cs"),
    ("OrderTotalsHelper.cs", "EliteRestaurant.Core/Utils/OrderTotalsHelper.cs"),
    ("OrderPrepTimeEstimator.cs", "EliteRestaurant.Core/Utils/OrderPrepTimeEstimator.cs"),
    ("OrderTotalsCalculator.cs", "EliteRestaurant.Core/Utils/OrderTotalsCalculator.cs"),
    ("OrderWorkflow.cs", "EliteRestaurant.Core/Utils/OrderWorkflow.cs"),
    ("OrderRecordQueryExtensions.cs", "EliteRestaurant.Core/Data/OrderRecordQueryExtensions.cs"),
    ("DataReconciler.cs", "EliteRestaurant.Core/Data/DataReconciler.cs"),
    ("SharedOrderDraftStore.cs", "EliteRestaurant.Core/Utils/SharedOrderDraftStore.cs"),
    ("OrderSubmissionHelper.cs", "EliteRestaurant.Core/Orders/OrderSubmissionHelper.cs"),
    ("AppDbContext.cs", "EliteRestaurant.Core/Data/AppDbContext.cs"),
]

body_core = "".join(pre_block(rel, rel, read_rel(rel)) for _, rel in core_files)

pro_files = [
    ("CreateOrderSubmissionModels.cs", "EliteRestaurantPro/Services/CreateOrderSubmissionModels.cs"),
    ("TableLoadingService.cs", "EliteRestaurantPro/Services/TableLoadingService.cs"),
    ("DraftPersistenceService.cs", "EliteRestaurantPro/Services/DraftPersistenceService.cs"),
    ("OrderSubmissionService.cs", "EliteRestaurantPro/Services/OrderSubmissionService.cs"),
]

body_pro = "<h2 id=\"pro-listings\">7. Desktop services — full source listings</h2>\n" + "".join(
    pre_block(rel, rel, read_rel(rel)) for _, rel in pro_files
)

body_vm = (
    "<h3>CreateOrderViewModel.cs (complete file)</h3>"
    '<p class="path"><code>EliteRestaurantPro/ViewModels/CreateOrderViewModel.cs</code> — full orchestration: services, '
    "<code>LoadData</code>, <code>RecalculateTotals</code>, <code>CreateOrder</code>, drafts.</p>"
    f"<pre><code>{esc(read_rel('EliteRestaurantPro/ViewModels/CreateOrderViewModel.cs'))}</code></pre>\n"
)

api_section = """
<h2 id="api-listings">8. API &amp; configuration — full source listings</h2>
"""

api_files = [
    ("Program.cs", "EliteRestaurant.Api/Program.cs"),
    ("AuthController.cs", "EliteRestaurant.Api/Controllers/AuthController.cs"),
    ("HealthController.cs", "EliteRestaurant.Api/Controllers/HealthController.cs"),
    ("TablesController.cs", "EliteRestaurant.Api/Controllers/TablesController.cs"),
    ("ReservationsController.cs", "EliteRestaurant.Api/Controllers/ReservationsController.cs"),
    ("ServerPortalController.cs", "EliteRestaurant.Api/Controllers/ServerPortalController.cs"),
    ("TabletAuthService.cs", "EliteRestaurant.Api/Security/TabletAuthService.cs"),
    ("CurrencyPricingOptions.cs", "EliteRestaurant.Api/CurrencyPricingOptions.cs"),
    ("appsettings.json", "EliteRestaurant.Api/appsettings.json"),
]

body_api_parts = [pre_block(title, rel, read_rel(rel)) for title, rel in api_files]

body_api = api_section + "".join(body_api_parts)

safety = "<h2 id=\"safety\">9. Operational safety — SeedRunner (complete <code>Program.cs</code>)</h2>" + pre_block(
    "SeedRunner/Program.cs",
    "SeedRunner/Program.cs",
    read_rel("SeedRunner/Program.cs"),
)

desktop_ui = (
    "<h2>9b. WPF database bootstrap — App.xaml.cs + DatabasePipeSetupDialog</h2>"
    + pre_block("App.xaml.cs", "EliteRestaurantPro/App.xaml.cs", read_rel("EliteRestaurantPro/App.xaml.cs"))
    + pre_block(
        "DatabasePipeSetupDialog.xaml",
        "EliteRestaurantPro/Views/DatabasePipeSetupDialog.xaml",
        read_rel("EliteRestaurantPro/Views/DatabasePipeSetupDialog.xaml"),
    )
    + pre_block(
        "DatabasePipeSetupDialog.xaml.cs",
        "EliteRestaurantPro/Views/DatabasePipeSetupDialog.xaml.cs",
        read_rel("EliteRestaurantPro/Views/DatabasePipeSetupDialog.xaml.cs"),
    )
)

tests_section = "<h2 id=\"tests\">10. Automated tests — full listings</h2>"
test_files = [
    ("DataReconcilerReconcileTests.cs", "EliteRestaurant.Tests/DataReconcilerReconcileTests.cs"),
    ("DataReconcilerTests.cs", "EliteRestaurant.Tests/DataReconcilerTests.cs"),
    ("OrderDiscountParserTests.cs", "EliteRestaurant.Tests/OrderDiscountParserTests.cs"),
    ("OrderPrepTimeEstimatorTests.cs", "EliteRestaurant.Tests/OrderPrepTimeEstimatorTests.cs"),
    ("OrderSubmissionHelperTests.cs", "EliteRestaurant.Tests/OrderSubmissionHelperTests.cs"),
    ("OrderTotalsCalculatorTests.cs", "EliteRestaurant.Tests/OrderTotalsCalculatorTests.cs"),
    ("OrderWorkflowTests.cs", "EliteRestaurant.Tests/OrderWorkflowTests.cs"),
    ("PayrollCalculatorTests.cs", "EliteRestaurant.Tests/PayrollCalculatorTests.cs"),
    ("StaffPortalAuthenticationTests.cs", "EliteRestaurant.Tests/StaffPortalAuthenticationTests.cs"),
]
body_tests = "".join(pre_block(rel, rel, read_rel(rel)) for _, rel in test_files)

closing = """
<h2 id="risks">11. Residual risks &amp; recommendations</h2>
<div class="warn">
<p><strong>Operational:</strong> Any process with PostgreSQL superuser or broad DDL/DML rights can still destroy data. Confirmation gates reduce accidental use; they do not stop malicious use.</p>
<p><strong>Technical:</strong> Large ViewModels outside create-order; API/Settings pricing precedence must stay documented; expand tests over order completion and ledger posting.</p>
</div>
<p class="meta"><em>Regenerate this document after major refactors: <code>python docs/_generate_full_review_docs.py</code></em></p>
</div>
"""

html_review = f"""<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Elite Restaurant — Full Code Review (with source)</title>
<style>{css}</style>
</head>
<body>
{narrative_intro}
{body_core}
{body_pro}
{body_vm}
{body_api}
{safety}
{desktop_ui}
{tests_section}
{body_tests}
{closing}
</body>
</html>
"""

# --- Document 2: Architecture ---
arch_intro = """
<div class="doc">
<header>
<h1>Elite Restaurant — Full Architecture &amp; Refactor Document</h1>
<p class="meta"><strong>Purpose:</strong> Architecture for senior review with <strong>verbatim code</strong> (full <code>Program.cs</code>, <code>AppDbContext</code>, Server Portal HTML, WPF bootstrap, services, ViewModel, API auth/pricing, tests). <strong>Print length:</strong> often <strong>50–100+ pages</strong> as PDF.</p>
</header>

<nav class="toc">
<strong>Table of contents</strong>
<ul>
<li><a href="#a1">1. System context &amp; deployment</a></li>
<li><a href="#a2">2. Layering &amp; dependency rules</a></li>
<li><a href="#a3">3. PostgreSQL &amp; EF Core (implementation)</a></li>
<li><a href="#a4">4. ASP.NET Core API host</a></li>
<li><a href="#a-dbctx">4b. AppDbContext (full)</a></li>
<li><a href="#a5">5. CORS, HTTPS, static files</a></li>
<li><a href="#a6">6. Server Portal — HTML/CSS/JS (excerpt + patterns)</a></li>
<li><a href="#a7">7. WPF client architecture</a></li>
<li><a href="#a8">8. Create-order refactor (services + ViewModel)</a></li>
<li><a href="#a9">9. Configuration merge (API vs SettingsManager)</a></li>
<li><a href="#a10">10. Testing strategy</a></li>
<li><a href="#a11">11. Roadmap</a></li>
</ul>
</nav>

<h2 id="a1">1. System context &amp; deployment</h2>
<p>The system runs as: (1) Windows desktops executing <strong>EliteRestaurantPro</strong> against a shared <strong>PostgreSQL</strong> database; (2) one or more <strong>EliteRestaurant.Api</strong> instances on the LAN exposing JSON for tablets and serving <code>wwwroot/index.html</code>; (3) batch tools (<code>SeedRunner</code>) for environments where full reset is acceptable.</p>

<h2 id="a2">2. Layering &amp; dependency rules</h2>
<p><code>EliteRestaurant.Core</code> has no reference to WPF or ASP.NET. <code>EliteRestaurantPro</code> and <code>EliteRestaurant.Api</code> both depend on Core. Tests depend on Core only so CI avoids Windows-only WPF targets.</p>

<h2 id="a3">3. PostgreSQL &amp; EF Core</h2>
<p>Desktop and tools typically construct <code>new AppDbContext()</code> which uses <code>OnConfiguring</code> when options are not preset. The API registers <code>AddDbContextPool</code> with Npgsql + retry policy. Migrations live under Core; <code>DatabaseInitializer.Initialize()</code> runs on API startup.</p>
<div class="note">
The full <code>AppDbContext</code> is lengthy; inspect <code>EliteRestaurant.Core/Data/AppDbContext.cs</code> in the repository. This document embeds API registration and transactional patterns in the listings below.
</div>

<h2 id="a4">4. ASP.NET Core API host — full <code>Program.cs</code></h2>
"""

wwwroot = read_rel("EliteRestaurant.Api/wwwroot/index.html")
wwwroot_excerpt_full = wwwroot

portal_section = (
    '<h2 id="a5">5. CORS, HTTPS, static files, Swagger</h2>'
    "<p>Implemented in <code>Program.cs</code> (listed above and summarized here): strict <code>Cors:AllowedOrigins</code>; optional Kestrel HTTPS with PFX; <code>UseDefaultFiles</code> + <code>UseStaticFiles</code> for the Server Portal; Swagger enabled.</p>"
    '<h2 id="a6">6. Server Portal — HTML/CSS (first 120 lines of index.html)</h2>'
    "<p>The portal uses <strong>embedded CSS</strong> (dark theme, CSS grid) and vanilla JavaScript for tabs and fetch calls.</p>"
    + pre_block("index.html (excerpt)", "EliteRestaurant.Api/wwwroot/index.html", wwwroot_excerpt_full)
    + '<h2 id="a7">7. WPF client — startup &amp; database dialog (full listings)</h2>'
    + pre_block("App.xaml.cs", "EliteRestaurantPro/App.xaml.cs", read_rel("EliteRestaurantPro/App.xaml.cs"))
    + pre_block(
        "DatabasePipeSetupDialog.xaml",
        "EliteRestaurantPro/Views/DatabasePipeSetupDialog.xaml",
        read_rel("EliteRestaurantPro/Views/DatabasePipeSetupDialog.xaml"),
    )
    + pre_block(
        "DatabasePipeSetupDialog.xaml.cs",
        "EliteRestaurantPro/Views/DatabasePipeSetupDialog.xaml.cs",
        read_rel("EliteRestaurantPro/Views/DatabasePipeSetupDialog.xaml.cs"),
    )
    + '<h2 id="a8">8. Create-order services (full listings)</h2>'
    + "".join(pre_block(rel, rel, read_rel(rel)) for _, rel in pro_files)
    + "<h3>CreateOrderViewModel.cs (complete file)</h3>"
    + '<p class="path"><code>EliteRestaurantPro/ViewModels/CreateOrderViewModel.cs</code></p>'
    + "<pre><code>"
    + esc(read_rel("EliteRestaurantPro/ViewModels/CreateOrderViewModel.cs"))
    + "</code></pre>"
    + '<h2 id="a-api-more">8b. API — Tablet auth, pricing options, appsettings</h2>'
    + pre_block("TabletAuthService.cs", "EliteRestaurant.Api/Security/TabletAuthService.cs", read_rel("EliteRestaurant.Api/Security/TabletAuthService.cs"))
    + pre_block("CurrencyPricingOptions.cs", "EliteRestaurant.Api/CurrencyPricingOptions.cs", read_rel("EliteRestaurant.Api/CurrencyPricingOptions.cs"))
    + pre_block("appsettings.json", "EliteRestaurant.Api/appsettings.json", read_rel("EliteRestaurant.Api/appsettings.json"))
    + '<h2 id="a9">9. ServerPortalController (complete)</h2>'
    + pre_block(
        "ServerPortalController.cs",
        "EliteRestaurant.Api/Controllers/ServerPortalController.cs",
        read_rel("EliteRestaurant.Api/Controllers/ServerPortalController.cs"),
    )
    + '<h2 id="a10">10. Testing strategy (full listings)</h2>'
    + "<p>Tests reference Core only so they run on any CI agent without WPF.</p>"
    + "".join(pre_block(rel, rel, read_rel(rel)) for _, rel in test_files)
    + '<h2 id="a11">11. Roadmap</h2>'
    + "<ul>"
    + "<li>API integration tests (<code>WebApplicationFactory</code>).</li>"
    + "<li>Further ViewModel decomposition (money, reports, kitchen).</li>"
    + "<li>Optional: extract Server Portal CSS/JS to separate files with a small build step.</li>"
    + "</ul>"
    + '<p class="meta"><em>End of architecture document. Regenerate via <code>python docs/_generate_full_review_docs.py</code></em></p>'
    + "</div>"
)

db_context_block = (
    '<h2 id="a-dbctx">4b. AppDbContext (complete — EF model &amp; configuration)</h2>'
    + pre_block("AppDbContext.cs", "EliteRestaurant.Core/Data/AppDbContext.cs", read_rel("EliteRestaurant.Core/Data/AppDbContext.cs"))
)

html_arch = (
    "<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n"
    '<meta name="viewport" content="width=device-width, initial-scale=1">\n'
    "<title>Elite Restaurant — Full Architecture Document</title>\n<style>"
    + css
    + "</style>\n</head>\n<body>\n"
    + arch_intro
    + pre_block("Program.cs (complete)", "EliteRestaurant.Api/Program.cs", read_rel("EliteRestaurant.Api/Program.cs"))
    + db_context_block
    + portal_section
    + "\n</body>\n</html>\n"
)

out1 = ROOT / "docs" / "Senior-Code-Review-EliteRestaurant-FULL.html"
out2 = ROOT / "docs" / "Architecture-and-Refactor-EliteRestaurant-FULL.html"
out1.write_text(html_review, encoding="utf-8")
out2.write_text(html_arch, encoding="utf-8")
print("Wrote", out1, "and", out2)
