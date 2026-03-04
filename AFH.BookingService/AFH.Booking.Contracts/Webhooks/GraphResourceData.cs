namespace AFH.Common.CalendarUtils.Sdk.Contracts.Webhooks;

public sealed class GraphResourceData
{
    public string? Id { get; set; }               // event id
    public string? OdataType { get; set; }        // "#Microsoft.Graph.Event"
}
