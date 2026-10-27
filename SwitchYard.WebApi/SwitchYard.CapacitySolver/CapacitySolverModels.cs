namespace SwitchYard.CapacitySolver;

public static class CapacitySolverModels
{
    public static CapacityModelRegistry CreateRegistry() =>
        new(new ICapacityModel[] { new StationCapacityModel() });
}
