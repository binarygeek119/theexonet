namespace Theexonet.Core.Enums;

public enum ShipClass
{
    Scout,
    Hauler,
    Freighter,
    Bulk,
}

public enum ShippingRouteTier
{
    Express,
    Standard,
    Economy,
}

public enum ShipmentStatus
{
    Scheduled,
    InTransit,
    Delayed,
    Arrived,
    Failed,
    Cancelled,
}
