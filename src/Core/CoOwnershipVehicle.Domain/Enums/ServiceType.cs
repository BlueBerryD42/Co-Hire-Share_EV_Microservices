namespace CoOwnershipVehicle.Domain.Enums;

/// <summary>
/// Types of maintenance services that can be performed on a vehicle
/// </summary>
public enum ServiceType
{
    /// <summary>
    /// Oil and filter change
    /// </summary>
    OilChange = 0,

    /// <summary>
    /// Tire rotation service
    /// </summary>
    TireRotation = 1,

    /// <summary>
    /// Brake system inspection and service
    /// </summary>
    BrakeInspection = 2,

    /// <summary>
    /// Battery check and replacement
    /// </summary>
    BatteryCheck = 3,

    /// <summary>
    /// Air filter replacement
    /// </summary>
    AirFilterReplacement = 4,

    /// <summary>
    /// Transmission service
    /// </summary>
    TransmissionService = 5,

    /// <summary>
    /// Coolant system service
    /// </summary>
    CoolantService = 6,

    /// <summary>
    /// Wheel alignment
    /// </summary>
    WheelAlignment = 7,

    /// <summary>
    /// Tire replacement
    /// </summary>
    TireReplacement = 8,

    /// <summary>
    /// Engine tune-up
    /// </summary>
    EngineTuneUp = 9,

    /// <summary>
    /// Windshield wiper replacement
    /// </summary>
    WiperReplacement = 10,

    /// <summary>
    /// Lighting system check and replacement
    /// </summary>
    LightingService = 11,

    /// <summary>
    /// Air conditioning service
    /// </summary>
    AirConditioningService = 12,

    /// <summary>
    /// General inspection
    /// </summary>
    GeneralInspection = 13,

    /// <summary>
    /// Suspension service
    /// </summary>
    SuspensionService = 14,

    /// <summary>
    /// Exhaust system service
    /// </summary>
    ExhaustService = 15,

    /// <summary>
    /// EV-specific: Battery health check
    /// </summary>
    EVBatteryCheck = 16,

    /// <summary>
    /// EV-specific: Charging system service
    /// </summary>
    EVChargingSystemService = 17,

    /// <summary>
    /// EV-specific: Software update
    /// </summary>
    EVSoftwareUpdate = 18,

    /// <summary>
    /// Other unspecified service
    /// </summary>
    Other = 99
}
