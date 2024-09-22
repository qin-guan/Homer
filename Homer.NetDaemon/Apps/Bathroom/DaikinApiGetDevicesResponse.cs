namespace Homer.NetDaemon.Apps.Bathroom;

public record DaikinApiGetDevicesResponse(
    int Code,
    string Message,
    string MessageCode,
    string Status,
    DaikinApiGetDevicesResponseResponse Response
);

public record DaikinApiGetDevicesResponseResponse(
    DaikinApiGetDevicesResponseResponseDevice[] Devices
);

public record DaikinApiGetDevicesResponseResponseDevice(
    int Id,
    string Name,
    string MacAddress,
    string FirmwareId,
    string FirmwareDateCode,
    string FirmwareVersion,
    string Model,
    string SerialNo,
    string Ssid,
    string IpAddress,
    bool Favourite,
    int CategoryId,
    string CategoryName,
    bool Notification,
    object[] Locations,
    bool Online,
    int Rssi,
    string User,
    bool IsOwner,
    object[] SharedPersons,
    DaikinApiGetDevicesResponseResponseDeviceData Data
);

public record DaikinApiGetDevicesResponseResponseDeviceData(
    int Mode,
    string ModeName,
    DaikinApiGetDevicesResponseResponseDeviceDataAuxStatus AuxStatus,
    object[] ErrorCodes,
    string RelayState,
    double TmpLogTime,
    double OnTimeMeter,
    int Temperature,
    string HeaterStatus,
    bool AntiBacterial,
    double PairingStatus,
    string TemperatureUnit,
    int CuSubTemperature,
    bool HeaterLastRequest,
    string PairingStatusName,
    int TemperatureSetting
);

public record DaikinApiGetDevicesResponseResponseDeviceDataAuxStatus(
    bool Rc0,
    bool Rc1,
    bool Rc2,
    bool CuOnline,
    string CuSensErr,
    bool CuValveState
);