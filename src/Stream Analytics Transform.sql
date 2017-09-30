WITH NowAndPrevious AS
(
    SELECT  TemperatureM0 = Temperature,
            TemperatureM1 = LAG(Temperature, 1) OVER (PARTITION BY IotHub.ConnectionDeviceId LIMIT DURATION(ss, 5) WHEN Temperature IS NOT NULL),
            TemperatureM2 = LAG(Temperature, 2) OVER (PARTITION BY IotHub.ConnectionDeviceId LIMIT DURATION(ss, 5) WHEN Temperature IS NOT NULL),
            HumidityM0 = Humidity,
            HumidityM1 = LAG(Humidity, 1) OVER (PARTITION BY IotHub.ConnectionDeviceId LIMIT DURATION(ss, 5) WHEN Humidity IS NOT NULL),
            HumidityM2 = LAG(Humidity, 2) OVER (PARTITION BY IotHub.ConnectionDeviceId LIMIT DURATION(ss, 5) WHEN Humidity IS NOT NULL),
            VibrationM0 = Vibration,
            VibrationM1 = LAG(Vibration, 1) OVER (PARTITION BY IotHub.ConnectionDeviceId LIMIT DURATION(ss, 5) WHEN Vibration IS NOT NULL),
            VibrationM2 = LAG(Vibration,2) OVER (PARTITION BY IotHub.ConnectionDeviceId LIMIT DURATION(ss, 5) WHEN Vibration IS NOT NULL),
            DeviceId = IotHub.ConnectionDeviceId,
            Name,
			EquipmentStatus,
            TimeM0 = Time,
            TimeM1 = LAG(Time, 1) OVER (PARTITION BY IotHub.ConnectionDeviceId LIMIT DURATION(ss, 5)),
            TimeM2 = LAG(Time, 2) OVER (PARTITION BY IotHub.ConnectionDeviceId LIMIT DURATION(ss, 5))
    FROM    IotHubInput TIMESTAMP BY Time
    
),
HumidEvents AS
(
    SELECT      EventType = 'Humidity',
                HumidityM0 AS CurrentValue,
                HumidityM2 AS BeforeThreshold,
                TimeM0 AS EventTime,
                TimeM2 AS BeforeThresholdTime,
                Duration = DATEDIFF(ss, TimeM2, TimeM0),
                Name,
                DeviceId,
				EquipmentStatus,
                (CASE WHEN HumidityM0 > 65 THEN 'CRITICAL' ELSE (CASE WHEN HumidityM1 > 60 THEN 'WARN' ELSE 'HEALTHY' END) END) AS Severity
    FROM        NowAndPrevious 
    WHERE       (HumidityM0 > 65 AND HumidityM1 > 65 AND HumidityM2 <65) OR 
                (HumidityM0 > 60 AND HumidityM1 > 60 AND HumidityM2 <60) OR 
                (HumidityM0 < 60 AND HumidityM1 < 60 AND HumidityM2 >60)
),
TemperatureEvents AS
(
    SELECT      EventType = 'Temperature',
                TemperatureM0 AS CurrentValue,
                TemperatureM2 AS BeforeThreshold,
                TimeM0 AS EventTime,
                TimeM2 AS BeforeThresholdTime,
                Duration = DATEDIFF(ss, TimeM2, TimeM0),
                Name,
                DeviceId,
				EquipmentStatus,
                (CASE WHEN TemperatureM0 > 45 THEN 'CRITICAL' ELSE (CASE WHEN TemperatureM0 > 30 THEN 'WARN' ELSE 'HEALTHY' END) END) AS Severity
    FROM        NowAndPrevious 
    WHERE       (TemperatureM0 > 30 AND TemperatureM1 > 30 AND TemperatureM2 <30) OR 
                (TemperatureM0 > 45 AND TemperatureM1 > 45 AND TemperatureM2 < 45) OR 
                (TemperatureM0 < 30 AND TemperatureM1 < 30 AND TemperatureM2 >30)
),
VibrationEvents AS
(
    SELECT      EventType = 'Vibration',
                VibrationM0 AS CurrentValue,
                VibrationM2 AS BeforeThreshold,
                TimeM0 AS EventTime,
                TimeM2 AS BeforeThresholdTime,
                Duration = DATEDIFF(ss, TimeM2, TimeM0),
                Name,
                DeviceId,
				EquipmentStatus,
                (CASE WHEN VibrationM0 > 2 THEN 'CRITICAL' ELSE (CASE WHEN VibrationM0 > 1 THEN 'WARN' ELSE 'HEALTHY' END) END) AS Severity
    FROM        NowAndPrevious 
    WHERE       (VibrationM0 > 1 AND VibrationM1 > 1 AND VibrationM2 < 1) OR 
                (VibrationM0 > 2 AND VibrationM1 > 2 AND VibrationM2 < 2) OR 
                (VibrationM0 < 1 AND VibrationM1 < 1 AND VibrationM2 > 1)
),
AllEvents AS
(
    SELECT * FROM HumidEvents UNION SELECT * FROM TemperatureEvents UNION SELECT * FROM VibrationEvents
)

SELECT * INTO EventHubOutput FROM AllEvents
SELECT * INTO BlobSink FROM IotHubInput TIMESTAMP BY Time
--SELECT * INTO PbiDataset FROM IotHubInput TIMESTAMP BY Time