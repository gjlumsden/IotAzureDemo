@maxLength(7)
@description('The App Name to base resource names upon.')
param appName string

@allowed([
  'Standard_LRS'
  'Standard_GRS'
  'Standard_RAGRS'
  'Premium_LRS'
])
@description('The storage SKU to use for storage accounts. Applies to all storage accounts in the deployment')
param storageSKU string = 'Standard_LRS'

@allowed([
  'F1'
  'S1'
  'S2'
  'S3'
])
@description('The SKU for the IoT Hub.')
param iotHubSKU string = 'F1'

@allowed([
  'latest'
  '~1'
  '~2'
  '~3'
  'beta'
])
@description('The version of the Function App runtime to use')
param FUNCTIONS_EXTENSION_VERSION string = '~1'
param SendgridKey string

@minLength(6)
param emailRecipients string
param alertSenderName string
param alertSenderAddress string

var functionAppStorageName_var = toLower('${appName}func${uniqueString(resourceGroup().id)}')
var archiveStorageName_var = toLower('${appName}arch${uniqueString(resourceGroup().id)}')
var eventAlertStorageName_var = toLower('${appName}qstr${uniqueString(resourceGroup().id)}')
var iotHubName_var = '${appName}-IoT-Hub-${uniqueString(resourceGroup().id)}'
var functionAppName_var = '${appName}-Functions-${uniqueString(resourceGroup().id)}'
var hostingPlanName_var = '${appName}-Plan-${uniqueString(resourceGroup().id)}'
var appInsightsName_var = '${appName}-Functions-AI-${uniqueString(resourceGroup().id)}'
var eventHubNamespace_var = '${appName}-Event-Hub-${uniqueString(resourceGroup().id)}'
var asaJobName_var = '${appName}-asa-job-${uniqueString(resourceGroup().id)}'
var cosmosAccountName_var = '${toLower(appName)}-cosmosdb-${uniqueString(resourceGroup().id)}'
var eventHubName = 'telemetry'
var sensorEventFuncSASKeyName = 'sensorEventFuncKey'
var asaEventSendSASKey = 'asaJobEventSendKey'
var sensorEventFuncConsumerGroup = 'sensorEventFunctionCg'
var iotHubAsaRawOutputConsumerGroup = 'asaRawOutputCG'
var iotHubAsaAggregateOutputConsumerGroup = 'asaAggOutputCG'
var IotHubAsaKeyName = 'asaJobKey'
var IotHubFunctionServiceConnectKeyName = 'funcServiceKey'
var rawDataContainerName = 'rawdataarchive'
var asaJobTransformQuery = 'WITH NowAndPrevious AS\r\n(\r\n    SELECT  TemperatureM0 = Temperature,\r\n            TemperatureM1 = LAG(Temperature, 1) OVER (PARTITION BY IotHub.ConnectionDeviceId LIMIT DURATION(ss, 5) WHEN Temperature IS NOT NULL),\r\n            TemperatureM2 = LAG(Temperature, 2) OVER (PARTITION BY IotHub.ConnectionDeviceId LIMIT DURATION(ss, 5) WHEN Temperature IS NOT NULL),\r\n            HumidityM0 = Humidity,\r\n            HumidityM1 = LAG(Humidity, 1) OVER (PARTITION BY IotHub.ConnectionDeviceId LIMIT DURATION(ss, 5) WHEN Humidity IS NOT NULL),\r\n            HumidityM2 = LAG(Humidity, 2) OVER (PARTITION BY IotHub.ConnectionDeviceId LIMIT DURATION(ss, 5) WHEN Humidity IS NOT NULL),\r\n            VibrationM0 = Vibration,\r\n            VibrationM1 = LAG(Vibration, 1) OVER (PARTITION BY IotHub.ConnectionDeviceId LIMIT DURATION(ss, 5) WHEN Vibration IS NOT NULL),\r\n            VibrationM2 = LAG(Vibration,2) OVER (PARTITION BY IotHub.ConnectionDeviceId LIMIT DURATION(ss, 5) WHEN Vibration IS NOT NULL),\r\n            DeviceId = IotHub.ConnectionDeviceId,\r\n            Name,\r\n\t\t\tEquipmentStatus,\r\n            TimeM0 = Time,\r\n            TimeM1 = LAG(Time, 1) OVER (PARTITION BY IotHub.ConnectionDeviceId LIMIT DURATION(ss, 5)),\r\n            TimeM2 = LAG(Time, 2) OVER (PARTITION BY IotHub.ConnectionDeviceId LIMIT DURATION(ss, 5))\r\n    FROM    IotHubInput TIMESTAMP BY Time\r\n    \r\n),\r\nHumidEvents AS\r\n(\r\n    SELECT      EventType = \'Humidity\',\r\n                HumidityM0 AS CurrentValue,\r\n                HumidityM2 AS BeforeThreshold,\r\n                TimeM0 AS EventTime,\r\n                TimeM2 AS BeforeThresholdTime,\r\n                Duration = DATEDIFF(ss, TimeM2, TimeM0),\r\n                Name,\r\n                DeviceId,\r\n\t\t\t\tEquipmentStatus,\r\n                (CASE WHEN HumidityM0 > 65 THEN \'CRITICAL\' ELSE (CASE WHEN HumidityM1 > 60 THEN \'WARN\' ELSE \'HEALTHY\' END) END) AS Severity\r\n    FROM        NowAndPrevious \r\n    WHERE       (HumidityM0 > 65 AND HumidityM1 > 65 AND HumidityM2 <65) OR \r\n                (HumidityM0 > 60 AND HumidityM1 > 60 AND HumidityM2 <60) OR \r\n                (HumidityM0 < 60 AND HumidityM1 < 60 AND HumidityM2 >60)\r\n),\r\nTemperatureEvents AS\r\n(\r\n    SELECT      EventType = \'Temperature\',\r\n                TemperatureM0 AS CurrentValue,\r\n                TemperatureM2 AS BeforeThreshold,\r\n                TimeM0 AS EventTime,\r\n                TimeM2 AS BeforeThresholdTime,\r\n                Duration = DATEDIFF(ss, TimeM2, TimeM0),\r\n                Name,\r\n                DeviceId,\r\n\t\t\t\tEquipmentStatus,\r\n                (CASE WHEN TemperatureM0 > 45 THEN \'CRITICAL\' ELSE (CASE WHEN TemperatureM0 > 30 THEN \'WARN\' ELSE \'HEALTHY\' END) END) AS Severity\r\n    FROM        NowAndPrevious \r\n    WHERE       (TemperatureM0 > 30 AND TemperatureM1 > 30 AND TemperatureM2 <30) OR \r\n                (TemperatureM0 > 45 AND TemperatureM1 > 45 AND TemperatureM2 < 45) OR \r\n                (TemperatureM0 < 30 AND TemperatureM1 < 30 AND TemperatureM2 >30)\r\n),\r\nVibrationEvents AS\r\n(\r\n    SELECT      EventType = \'Vibration\',\r\n                VibrationM0 AS CurrentValue,\r\n                VibrationM2 AS BeforeThreshold,\r\n                TimeM0 AS EventTime,\r\n                TimeM2 AS BeforeThresholdTime,\r\n                Duration = DATEDIFF(ss, TimeM2, TimeM0),\r\n                Name,\r\n                DeviceId,\r\n\t\t\t\tEquipmentStatus,\r\n                (CASE WHEN VibrationM0 > 2 THEN \'CRITICAL\' ELSE (CASE WHEN VibrationM0 > 1 THEN \'WARN\' ELSE \'HEALTHY\' END) END) AS Severity\r\n    FROM        NowAndPrevious \r\n    WHERE       (VibrationM0 > 1 AND VibrationM1 > 1 AND VibrationM2 < 1) OR \r\n                (VibrationM0 > 2 AND VibrationM1 > 2 AND VibrationM2 < 2) OR \r\n                (VibrationM0 < 1 AND VibrationM1 < 1 AND VibrationM2 > 1)\r\n),\r\nAllEvents AS\r\n(\r\n    SELECT * FROM HumidEvents UNION SELECT * FROM TemperatureEvents UNION SELECT * FROM VibrationEvents\r\n)\r\n\r\nSELECT * INTO EventHubOutput FROM AllEvents\r\nSELECT * INTO BlobSink FROM IotHubInput TIMESTAMP BY Time\r\n--SELECT * INTO PbiDataset FROM IotHubInput TIMESTAMP BY Time'

resource functionAppStorageName 'Microsoft.Storage/storageAccounts@2019-06-01' = {
  name: functionAppStorageName_var
  sku: {
    name: storageSKU
  }
  kind: 'Storage'
  location: resourceGroup().location
  tags: {
    DisplayName: 'Functions Storage'
  }
  properties: {
    encryption: {
      services: {
        blob: {
          enabled: true
        }
      }
      keySource: 'Microsoft.Storage'
    }
  }
}

resource eventAlertStorageName 'Microsoft.Storage/storageAccounts@2019-06-01' = {
  name: eventAlertStorageName_var
  sku: {
    name: storageSKU
  }
  kind: 'Storage'
  location: resourceGroup().location
  tags: {
    DisplayName: 'Alert Storage Queues'
  }
  properties: {}
}

resource archiveStorageName 'Microsoft.Storage/storageAccounts@2019-06-01' = {
  name: archiveStorageName_var
  sku: {
    name: storageSKU
  }
  kind: 'BlobStorage'
  location: resourceGroup().location
  tags: {
    DisplayName: 'Archive Storage'
  }
  properties: {
    encryption: {
      services: {
        blob: {
          enabled: true
        }
      }
      keySource: 'Microsoft.Storage'
    }
    accessTier: 'Hot'
  }
}

resource iotHubName 'Microsoft.Devices/IotHubs@2020-08-01' = {
  name: iotHubName_var
  location: resourceGroup().location
  tags: {
    DisplayName: 'Ingestion IoT Hub'
  }
  properties: {
    location: resourceGroup().location
    authorizationPolicies: [
      {
        keyName: IotHubAsaKeyName
        rights: 'ServiceConnect'
      }
      {
        keyName: 'iotHubOwner'
        rights: 'RegistryRead, RegistryWrite, ServiceConnect, DeviceConnect'
      }
      {
        keyName: IotHubFunctionServiceConnectKeyName
        rights: 'RegistryRead, RegistryWrite, ServiceConnect'
      }
      {
        keyName: 'deviceConnect'
        rights: 'DeviceConnect'
      }
    ]
    features: 'DeviceManagement'
  }
  sku: {
    name: iotHubSKU
    capacity: 1
  }
}

resource iotHubName_events_iotHubAsaRawOutputConsumerGroup 'Microsoft.Devices/IotHubs/eventHubEndpoints/ConsumerGroups@2020-08-01' = {
  name: '${iotHubName_var}/events/${iotHubAsaRawOutputConsumerGroup}'
  dependsOn: [
    iotHubName
  ]
}

resource iotHubName_events_iotHubAsaAggregateOutputConsumerGroup 'Microsoft.Devices/IotHubs/eventHubEndpoints/ConsumerGroups@2020-08-01' = {
  name: '${iotHubName_var}/events/${iotHubAsaAggregateOutputConsumerGroup}'
  dependsOn: [
    iotHubName
  ]
}

resource hostingPlanName 'Microsoft.Web/serverfarms@2020-06-01' = {
  name: hostingPlanName_var
  location: resourceGroup().location
  tags: {
    DisplayName: 'Consumption Hosting Plan'
  }
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
    size: 'Y1'
    family: 'Y'
    capacity: 0
  }
  properties: {
    name: hostingPlanName_var
  }
  dependsOn: []
}

resource functionAppName 'Microsoft.Web/sites@2020-06-01' = {
  name: functionAppName_var
  location: resourceGroup().location
  kind: 'functionapp'
  tags: {
    DisplayName: '${appName} Function App'
  }
  properties: {
    name: functionAppName_var
    serverFarmId: hostingPlanName.id
    clientAffinityEnabled: false
    siteConfig: {
      appSettings: [
        {
          name: 'AzureWebJobsDashboard'
          value: 'DefaultEndpointsProtocol=https;AccountName=${functionAppStorageName_var};AccountKey=${listKeys(functionAppStorageName.id, '2015-05-01-preview').key1}'
        }
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${functionAppStorageName_var};AccountKey=${listKeys(functionAppStorageName.id, '2015-05-01-preview').key1}'
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: FUNCTIONS_EXTENSION_VERSION
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${functionAppStorageName_var};AccountKey=${listKeys(functionAppStorageName.id, '2015-05-01-preview').key1}'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: '${toLower(functionAppName_var)}-prod'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: reference(appInsightsName.id, '2014-04-01').InstrumentationKey
        }
        {
          name: 'sensorEventConnectionString'
          value: listkeys(sensorEventFuncSASKeyName, '2015-08-01').primaryConnectionString
        }
        {
          name: 'alertStorageQueueConnection'
          value: 'DefaultEndpointsProtocol=https;AccountName=${eventAlertStorageName_var};AccountKey=${listKeys(eventAlertStorageName.id, '2015-05-01-preview').key1}'
        }
        {
          name: 'sendgridKey'
          value: SendgridKey
        }
        {
          name: 'DeviceManagementConnection'
          value: 'HostName=${iotHubName_var}.azure-devices.net;SharedAccessKeyName=${IotHubFunctionServiceConnectKeyName};SharedAccessKey=${listKeys(resourceId('Microsoft.Devices/IotHubs/Iothubkeys', iotHubName_var, IotHubFunctionServiceConnectKeyName), '2016-02-03').primaryKey}'
        }
        {
          name: 'iotHubName'
          value: iotHubName_var
        }
        {
          name: 'AzureWebJobsSecretStorageType'
          value: 'Blob'
        }
        {
          name: 'eventsConsumerGroup'
          value: sensorEventFuncConsumerGroup
        }
        {
          name: 'alertsQueueName'
          value: 'eventalerts'
        }
        {
          name: 'alertSenderName'
          value: alertSenderName
        }
        {
          name: 'alertSenderAddress'
          value: alertSenderAddress
        }
        {
          name: 'emailAlertRecipients'
          value: emailRecipients
        }
        {
          name: 'cosmosDbConnectionString'
          value: 'AccountEndpoint=${reference('Microsoft.DocumentDb/databaseAccounts/${cosmosAccountName_var}').documentEndpoint};AccountKey=${listKeys(cosmosAccountName.id, '2015-11-06').primaryMasterKey}'
        }
        {
          name: 'EmailEnabled'
          value: 'true'
        }
      ]
    }
  }
  dependsOn: [
    resourceId('Microsoft.EventHub/namespaces/eventhubs/authorizationRules', eventHubNamespace_var, eventHubName, sensorEventFuncSASKeyName)
    iotHubName
  ]
}

resource functionAppName_web 'Microsoft.Web/sites/config@2016-08-01' = {
  name: '${functionAppName.name}/web'
  properties: {
    use32BitWorkerProcess: false
  }
}

resource appInsightsName 'Microsoft.Insights/components@2020-02-02-preview' = {
  name: appInsightsName_var
  location: resourceGroup().location
  tags: {
    'hidden-link:${functionAppName.id}': 'Resource'
    DisplayName: 'Production AI Monitoring'
  }
  properties: {
    ApplicationId: functionAppName_var
  }
  dependsOn: []
}

resource eventHubNamespace 'Microsoft.EventHub/namespaces@2018-01-01-preview' = {
  name: eventHubNamespace_var
  location: resourceGroup().location
  properties: {}
  sku: {
    name: 'Standard'
    tier: 'Standard'
    capacity: 1
  }
  tags: {
    DisplayName: 'Event Hub Namespace'
  }
}

resource eventHubNamespace_eventHubName 'Microsoft.EventHub/namespaces/eventhubs@2017-04-01' = {
  name: '${eventHubNamespace.name}/${eventHubName}'
  location: resourceGroup().location
  properties: {
    messageRetentionInDays: 7
    partitionCount: 4
    path: eventHubName
  }
}

resource eventHubNamespace_eventHubName_sensorEventFuncConsumerGroup 'Microsoft.EventHub/namespaces/eventhubs/consumergroups@2017-04-01' = {
  location: resourceGroup().location
  name: '${eventHubNamespace_eventHubName.name}/${sensorEventFuncConsumerGroup}'
  properties: {
    eventHubPath: eventHubName
  }
}

resource eventHubNamespace_eventHubName_sensorEventFuncSASKeyName 'Microsoft.EventHub/namespaces/eventhubs/authorizationRules@2017-04-01' = {
  name: '${eventHubNamespace_eventHubName.name}/${sensorEventFuncSASKeyName}'
  location: resourceGroup().location
  properties: {
    rights: [
      'Listen'
    ]
  }
}

resource eventHubNamespace_eventHubName_asaEventSendSASKey 'Microsoft.EventHub/namespaces/eventhubs/authorizationRules@2017-04-01' = {
  name: '${eventHubNamespace_eventHubName.name}/${asaEventSendSASKey}'
  location: resourceGroup().location
  properties: {
    rights: [
      'Send'
    ]
  }
  dependsOn: [
    resourceId('Microsoft.EventHub/namespaces/eventhubs/authorizationRules', eventHubNamespace_var, eventHubName, sensorEventFuncSASKeyName)
  ]
}

resource asaJobName 'Microsoft.StreamAnalytics/streamingjobs@2017-04-01-preview' = {
  name: asaJobName_var
  location: resourceGroup().location
  tags: {
    DisplayName: 'Event Analysis Jobs'
  }
  properties: {
    sku: {
      name: 'Standard'
    }
    outputErrorPolicy: 'Stop'
    outputStartMode: 'JobStartTime'
    dataLocale: 'en-GB'
    eventsOutOfOrderPolicy: 'Adjust'
    eventsOutOfOrderMaxDelayInSeconds: 5
    eventsLateArrivalMaxDelayInSeconds: 1
    inputs: [
      {
        name: 'IotHubInput'
        properties: {
          type: 'stream'
          serialization: {
            type: 'JSON'
            properties: {
              encoding: 'UTF8'
            }
          }
          datasource: {
            type: 'Microsoft.Devices/IotHubs'
            properties: {
              iotHubNamespace: iotHubName_var
              sharedAccessPolicyName: IotHubAsaKeyName
              sharedAccessPolicyKey: listKeys(resourceId('Microsoft.Devices/IotHubs/Iothubkeys', iotHubName_var, IotHubAsaKeyName), '2016-02-03').primaryKey
              endpoint: 'messages/events'
              consumerGroupName: iotHubAsaRawOutputConsumerGroup
            }
          }
        }
      }
    ]
    transformation: {
      name: asaJobName_var
      properties: {
        streamingUnits: 1
        query: asaJobTransformQuery
      }
    }
    outputs: [
      {
        name: 'BlobSink'
        properties: {
          serialization: {
            type: 'CSV'
            properties: {
              fieldDelimiter: ','
              encoding: 'UTF8'
            }
          }
          datasource: {
            type: 'Microsoft.Storage/Blob'
            properties: {
              storageAccounts: [
                {
                  accountName: archiveStorageName_var
                  accountKey: listKeys(archiveStorageName.id, '2015-06-15').key1
                }
              ]
              container: rawDataContainerName
              pathPattern: 'rawData/{date}'
              dateFormat: 'yyyy/MM/dd'
              timeFormat: 'HH'
            }
          }
        }
      }
      {
        name: 'EventHubOutput'
        properties: {
          serialization: {
            type: 'JSON'
            properties: {
              encoding: 'UTF8'
              format: 'array'
            }
          }
          datasource: {
            type: 'Microsoft.ServiceBus/EventHub'
            properties: {
              eventHubName: eventHubName
              serviceBusNamespace: eventHubNamespace_var
              sharedAccessPolicyName: asaEventSendSASKey
              sharedAccessPolicyKey: listKeys(resourceId('Microsoft.Eventhub/namespaces/eventhubs/authorizationRules', eventHubNamespace_var, eventHubName, asaEventSendSASKey), '2015-08-01').primaryKey
              partitionKey: 'deviceId'
            }
          }
        }
      }
    ]
  }
  dependsOn: [
    iotHubName

    eventHubNamespace
    eventHubNamespace_eventHubName
    resourceId('Microsoft.EventHub/namespaces/eventhubs/authorizationRules', eventHubNamespace_var, eventHubName, asaEventSendSASKey)
  ]
}

resource cosmosAccountName 'Microsoft.DocumentDB/databaseAccounts@2020-04-01' = {
  name: cosmosAccountName_var
  location: resourceGroup().location
  tags: {
    DisplayName: 'CosmosDB Event Storage'
  }
  properties: {
    name: cosmosAccountName_var
    databaseAccountOfferType: 'Standard'
    locations: [
      {
        locationName: resourceGroup().location
        failoverPriority: 0
      }
    ]
  }
}

output functionAppName string = functionAppName_var