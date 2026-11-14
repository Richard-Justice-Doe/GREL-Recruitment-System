using Grsms.Tests.Infrastructure;

namespace Grsms.Tests;

/// <summary>docs/37 C6: the "one primary button per page" check itself (it runs on every record page the web tests open).</summary>
public class PrimaryActionCheckTests
{
    [Fact]
    public void Counts_filled_buttons_in_the_main_area_but_not_checklist_rows_dialogs_or_outline_buttons()
    {
        const string page = """
            <nav><a class="btn btn-primary">Menu</a></nav>
            <main>
              <a href="#form" class="btn btn-outline-primary">Go</a>
              <button type="submit" class="btn btn-success"><i class="fa"></i> Approve hire</button>
              <details class="doc-item tone-x" id="doc-1"><summary>Doc</summary>
                <details class="mt-1"><summary>Reject</summary></details>
                <button class="btn btn-sm btn-success">Accept</button>
              </details>
              <div class="modal fade"><div class="modal-body"></div><div class="modal-footer"><button class="btn btn-primary">OK</button></div></div>
              <a class="btn btn-sm btn-primary">Shortlisting</a>
            </main>
            """;
        Assert.Equal(["Approve hire", "Shortlisting"], PrimaryActionCheck.FilledButtons(page));
    }
}
