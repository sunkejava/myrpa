using AgentRPA.NodeAgent.Execution;

namespace AgentRPA.Tests;

public sealed class ConfiguredWorkflowSiteAdapterTests
{
    private static ConfiguredSiteAdapterOptions Options() => new()
    {
        Code = "authorized-medical-test",
        BaseUrl = "https://portal.example.test/medical/",
        Paths = new() { ["@site.login"] = "login" },
        Selectors = new() { ["@person.id"] = "[name=personId]" }
    };

    [Fact]
    public void Semantic_page_stays_on_the_configured_site()
    {
        var adapter = new ConfiguredWorkflowSiteAdapter(Options());
        Assert.Equal("https://portal.example.test/medical/login",
            adapter.ResolveUrl("@site.login", new Dictionary<string, string?>()));
    }

    [Fact]
    public void Cross_site_page_mapping_is_rejected_during_configuration()
    {
        var options = Options();
        options.Paths["@site.login"] = "https://untrusted.example.test/login";
        Assert.Throws<InvalidOperationException>(() => new ConfiguredWorkflowSiteAdapter(options));
    }

    [Fact]
    public void Absolute_url_to_another_origin_is_rejected_during_execution()
    {
        var adapter = new ConfiguredWorkflowSiteAdapter(Options());
        Assert.Throws<InvalidOperationException>(() =>
            adapter.ResolveUrl("https://untrusted.example.test/login", new Dictionary<string, string?>()));
    }

    [Fact]
    public void Selector_mapping_uses_explicit_semantic_key_and_rejects_unknown_keys()
    {
        var adapter = new ConfiguredWorkflowSiteAdapter(Options());
        Assert.Equal("[name=personId]", adapter.ResolveSelector("@person.id"));
        Assert.Throws<InvalidOperationException>(() => adapter.ResolveSelector("@person.unknown"));
    }
}
