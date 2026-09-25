using AgentRPA.Domain.Resources;

namespace AgentRPA.Tests;

public sealed class BusinessResourcesTests
{
    [Fact]
    public void Resource_codes_are_normalized_and_parent_ids_are_preserved()
    {
        var city = new City("cn-sd-qd", "青岛市");
        var system = new BusinessSystem(city.Id, "social", "社保系统", "https://example.test");
        var function = new BusinessFunction(system.Id, "employee.add", "增员");

        Assert.Equal("CN-SD-QD", city.Code);
        Assert.Equal(city.Id, system.CityId);
        Assert.Equal("SOCIAL", system.Code);
        Assert.Equal(system.Id, function.SystemId);
        Assert.Equal("EMPLOYEE.ADD", function.Code);
    }

    [Fact]
    public void Invalid_resource_names_and_missing_parents_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => new City(" ", "青岛市"));
        Assert.Throws<ArgumentException>(() => new BusinessSystem(Guid.Empty, "social", "社保", null));
        Assert.Throws<ArgumentException>(() => new BusinessFunction(Guid.Empty, "add", "增员"));
    }

    [Fact]
    public void Disabling_city_or_system_updates_the_resource_state()
    {
        var city = new City("cn-sd-qd", "青岛市");
        var system = new BusinessSystem(city.Id, "social", "社保系统", null);
        city.SetEnabled(false);
        system.SetEnabled(false);

        Assert.False(city.Enabled);
        Assert.False(system.Enabled);
    }
}
