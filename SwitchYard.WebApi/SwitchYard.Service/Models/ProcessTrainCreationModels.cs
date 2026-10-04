using SwitchYard.Capacity;

namespace SwitchYard.Service.Models;

public sealed class ProcessTrainCreationRequest : ProcessScope
{
    public string ProcessTemplateID { get; set; } = "";
    public int Revision { get; set; }
    public TrainRow? Train { get; set; }
    public string OriginTime { get; set; } = "";
    public string EndTime { get; set; } = "";
    public List<ProcessTrainActivitySelection> Selections { get; set; } = new();
}

public sealed class ProcessTrainActivitySelection
{
    public string ActivityID { get; set; } = "";
    public string? RouteID { get; set; }
    public string? TrackID { get; set; }
}

public sealed class ProcessTrainCreationResponse
{
    public TrainRow Train { get; set; } = new();
    public List<MovementRow> Movements { get; set; } = new();
    public TrainProcessSnapshot ProcessConstraint { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
