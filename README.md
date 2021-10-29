## CI Status

[![Coverage Status](https://coveralls.io/repos/github/SMI/SmiServices/badge.svg)](https://coveralls.io/github/SMI/SmiServices) ![GitHub](https://img.shields.io/github/license/SMI/SmiServices) [![Total alerts](https://img.shields.io/lgtm/alerts/g/SMI/SmiServices.svg?logo=lgtm&logoWidth=18)](https://lgtm.com/projects/g/SMI/SmiServices/alerts/)

| OS      | CI Pipeline |
| :---        |    :----:   |
| Windows      | [![Build Status](https://dev.azure.com/SmiOps/Public/_apis/build/status/SmiServices%20Windows?branchName=master)](https://dev.azure.com/SmiOps/Public/_build/latest?definitionId=4&branchName=master)       |
| Linux   | [![Build Status](https://dev.azure.com/SmiOps/Public/_apis/build/status/SmiServices%20Linux?branchName=master)](https://dev.azure.com/SmiOps/Public/_build/latest?definitionId=3&branchName=master) |


Version: `4.0.0`

# SMI Services

![loaddiagram](./docs/Images/SmiFlow.svg)

A suite of microservices for [loading*](./Glossary.md#loading), anonymising, linking and extracting [large volumnes](#scalability) of [dicom] medical images to support medical research.

The platform allows [dicom tags] (extracted from clinical images) to be loaded into MongoDB and relational database tables for the purposes of generating anonymous linked research extracts (including image anonymisation).

The latest binaries can be downloaded from the [releases section](https://github.com/SMI/SmiServices/releases/latest). See the instructions below on how to run the services.

## Contents
1. [Deployment](#deployment)
1. [Microservices](#microservices)
   1. [Data Load Microservices](#data-load-microservices)
   1. [Image Extraction Microservices](#image-extraction-microservices)
1. [Solution Overivew](#solution-overview)
1. [Building](#building)
1. [Running](#running)
1. [Testing](#testing)
1. [Dependencies](#dependencies)
1. [Scalability](#scalability)

## Deployment
The easiest way to use SmiServices is through the [Docker Image](https://github.com/jas88/smideploy).

For a more adaptable/scalable setup or to use existing infrastructure (databases etc), you will need to:

 - Install RabbitMq
 - Install MongoDb
 - Install a relational database (MySql, SqlServer, Postgres or Oracle)
 - [Install RDMP](https://github.com/HicServices/RDMP) and setup platform databases on a Sql Server.

After all services are in place:
 - Import the [queue definitions](./data/rabbitmqConfigs) into RabbitMQ via the admin web interface (or from command line).  Make any changes for vhost etc if desired
 - Download the software assets for the latest [SmiServices Release](https://github.com/SMI/SmiServices/releases) and unzip the appropriate service e.g. `smi-services-v4.0.0-linux-x64.tgz`

Configure 'default.yaml'
 - Update the credentials to match your RabbitMQ and MongoDb instance (you can ignore RDMP, Redis etc for now).
 - Remove the `TEST.` prefix on queue names

TODO: what else? test the software works, queue up a directory etc.

## Microservices

All microservices [follow the same design pattern](./src/common/Smi.Common/README.md).

The following  microservices have been written.  Microservices are loosely coupled, usually reading and writing only a single kind of message.  Each Queue and Exchange as implemented supports only one Type of `Smi.Common.Messages.IMessage`.

Microservices can be configured through [the configuration file](./data/microserviceConfigs/default.yaml).

A control queue is provided for controlling Microservices during runtime.  It supports a [limited number of commands](./docs/control-queues.md).

### Data Load Microservices

![loaddiagram](./docs/Images/LoadMicroservices.png)

| Microservice / Console App| Description |
| ------------- | ------------- |
| [ProcessDirectory]  | Command line application that finds dicom files on disk and [queues them for execution in RabbitMQ](./src/common/Smi.Common/Messages/AccessionDirectoryMessage.cs).|
| [DicomTagReader] | Opens queued dicom files on disk and [converts them to JSON](./src/common/Smi.Common/Messages/DicomFileMessage.cs).  Also creates a [summary record of the whole series](./src/common/Smi.Common/Messages/SeriesMessage.cs).|
| [IdentifierMapper] (Optional)  | Replaces the `PatientID` dicom Tag in a [DicomFileMessage] using a specified mapping table.|
| [MongoDBPopulator]  | Persists the dicom Tag data in [DicomFileMessage] and/or [SeriesMessage] into a MongoDB database document store. |
| [DicomRelationalMapper] | Persists the dicom Tag data (and file paths) in [DicomFileMessage] into a [relational database](https://github.com/HicServices/RDMP/blob/develop/Documentation/CodeTutorials/FAQ.md#databases).  ETL pipeline is controlled by an [RDMP] data load configuration.|
| [DicomReprocessor] | Runs a MongoDB query on the database populated by [MongoDBPopulator] and converts the results back into [DicomFileMessage] for (re)loading by [DicomRelationalMapper].|

### Image Extraction Microservices

![extractiondiagram](./docs/Images/ExtractionMicroservices.png)

| Microservice / Console App| Description |
| ------------- | ------------- |
| [IsIdentifiable]  | Evaluates data being prepared for extraction for personally identifiable data (PII).  See also [IsIdentifiableReviewer]|
| [ExtractImages] | Reads UIDs from a CSV file and generates [ExtractionRequestMessage] and audit message [ExtractionRequestInfoMessage].|
| [CohortExtractor] | Looks up SeriesInstanceUIDs in [ExtractionRequestMessage] and does relational database lookup(s) to resolve into physical image file location.  Generates  [ExtractFileMessage] and audit message [ExtractFileCollectionInfoMessage].|
| [CTPAnonymiser]  | Microservice wrapper for [CTP](https://github.com/johnperry/CTP).  Anonymises images specified in  [ExtractFileMessage] and copies to specified output directory.  Generates audit message [ExtractedFileStatusMessage].|
| [CohortPackager] | Records all audit messages and determines when jobs are complete.|

### Audit and Logging Systems

| Audit System | Description|
| ------------- | ------------- |
| [NLog](http://nlog-project.org/) | All Microservices log all activity to NLog, the manifestation of these logs can be to file/console/server etc as configured in the app.config file.|
| [Message Audit](./src/common/Smi.Common/README.md#logging) | Every message sent by a microservice has a unique Guid associated with it.  When a message is issued in response to an input message (all but the first message in a chain) the list of legacy message Guids is maintained.  This list is output as part of NLog logging.|
| [Data Load Audit](./src/microservices/Microservices.DicomRelationalMapper/Readme.md#7-audit)|The final Message Guid of every file identified for loading is recorded in the relational database image table.  In addition a valid from / data load ID field is recorded and any UPDATEs that take place (e.g. due to reprocessing a file) results in a persistence record being created in a shadow archive table.|
| [Extraction Audit (MongoDB)](./src/microservices/Microservices.CohortPackager/README.md) | CohortPackager is responsible for auditing extraction Info messages from all extraction services, recording which images have been requested and when image anonymisation has been completed.  This is currently implemented through `IExtractJobStore`.|
| CohortExtractor Audit | Obsolete interface `IAuditExtractions` previously existed to record the linkage results and patient release identifiers.|
| Fatal Error Logging | All Microservices that crash or log a fatal error are shut down and log a message to the Fatal Error Logging Exchange.  TODO: Nobody listens to this currently.|
| Quarantine | TODO: Doesn't exist yet.|

## Solution Overview

Apart from the Microservices (documented above) the following library classes are also included in the solution:

| Project Name | Path | Description|
| ------------- | ----- | ------------- |
| Dicom File Tester |/Applications| Application for testing DICOM files compatibility with Dicom<->JSON and Dicom to various database type conversions and back. It basically takes a file and pushes it through the various converters to see what breaks |
| Dicom Repopulator |/Applications| [See Microservices](#image-extraction-microservices) |
| Template Builder | /Applications| GUI tool for building modality database schema templates.  Supports viewing and exploring dicom tags in files|
| Smi.MongoDB.Common | /Reusable | Library containing methods for interacting with MongoDb |


## Building

### Building the C# Projects

Building requires a [.NET Core SDK](https://dotnet.microsoft.com/download/dotnet-core), at a compatible version as specified in the [`global.json`](/global.json) file.

To build the entire solution from the project root, run:

```bash
$ dotnet build [-r RID]
```

_The RID argument is optional. Use this if you want to build for a different platform e.g. `-r linux-x64` to build for Linux from a Windows machine. See [here](https://docs.microsoft.com/en-us/dotnet/core/rid-catalog) for more info on runtime identifiers._

To build an individual sub-project:

```bash
$ cd src/microservices/Microservices.DicomTagReader/
$ dotnet build
```

This will automatically rebuild any dependent projects which have changes as well.

### Building the Java Projects

Building the Java projects requires Java JDK `>= 1.7` (OpenJDK recommended 🙂), and Maven.

The CTP dependency first needs to be manually installed:

- Linux

```bash
$ cd lib/java/
$ ./installDat.sh
```

- Windows

```bash
$ cd lib\java\
$ .\installDat.bat
```

The projects can then be built and tested by returning to the top level directory and running:

```bash
$ mvn -f src/common/com.smi.microservices.parent/pom.xml clean test
```

This will compile and run the tests for the projects. The full test suite requires a local RabbitMQ server, however these can be skipped by passing `-PunitTests`. The entire test suite can be skipped by instead running `compile`, or by passing `-DskipTests`.

To build a single project and its dependencies, you can do:

```bash
$ mvn -f src/common/com.smi.microservices.parent/pom.xml test -pl com.smi.microservices:ctpanonymiser -am
```

Note: If you have Maven `>=3.6.1` then you can pass `-ntp` to each of the above commands in order to hide the large volume of messages related to the downloading of dependencies.

## Running

All applications and services are runnable through the `smi` program. This is available either in the binary distribution, or in the `src/Applications/Applications.SmiRunner/bin` directory if developing locally. See the SmiRunner [README](/src/applications/Applications.SmiRunner/README.md) for more information.

## Developing

### C# Projects

Development requires Visual Studio 2017 or later. Simply open the SmiServices.sln file.

To run the tests for IsIdentifiable, the Stanford NER classifier is required. This can be downloaded with the included script:

```bash
$ cd data/stanford-ner
$ ./download.sh
```

### Java Projects

Development requires Java JDK `>= 1.7`, and Maven.

### pre-commit

This repo uses [pre-commit] to manage and automatically run a series of linters
and code formatters. After cloning the repo and changing into the directory, run
this once to setup pre-commit.

```console
$ pip install pre-commit
$ pre-commit install
```

This will then run the checks before every commit. It can also be run manually
at any time:

```console
$ pre-commit run [<hook>] (--all-files | --files <file list>)
```

Running pre-commit locally is optional, since it is also run during any PR. To remove
pre-commit from your repo clone, simply run:

```console
$ pre-commit uninstall
```

## Testing

SMI is built using a microservices architecture and is primarily concerned with translating Dicom tag data into database records (in both MongoDb, Sql Server and MySql). Tests are split into those that:

- RequiresRelationalDb (Microsoft Sql Server / MySql)
- RequiresMongoDb (MongoDb)
- RequiresRabbit (RabbitMQ Server)
- Unit tests

Tests with the respective attributes will only run when these services exist in the test/development environment.  Connection strings/ports for these services can be found in:

- TestDatabases.txt (Relational Databases)
- default.yaml (RabbitMQ / MongoDb)
- Mongo.yaml
- Rabbit.yaml
- RelationalDatabases.yaml

For setting up the RDMP platform databases see https://github.com/HicServices/RDMP/blob/master/Documentation/CodeTutorials/Tests.md

## Note On Versioning

The C# projects share the same release version, which is controlled by the [SharedAssemblyInfo.cs](src/SharedAssemblyInfo.cs) file. The Java projects are versioned independently, set in their pom files, however in practice they follow the release version of the repo overall.

## Scalability

The services in this repository have been successfully used to load all medical imaging data captured in Scotland's National PACS archive.

Scalability is handled through parallel process execution (using [RabbitMQ]).  This allows slow processes (e.g. reading dicom tags from files on disk) to have more running instances while faster processes have less.  Scalability of large operations (e.g. linkage / cohort identification) is done within the [DBMS] layer.

## Dependencies

| Package | Status |
| :---        |    :----:   |
| [HIC.TypeGuesser](https://github.com/HicServices/TypeGuesser) |   [![Build, test and package](https://github.com/HicServices/TypeGuesser/actions/workflows/dotnet.yml/badge.svg)](https://github.com/HicServices/TypeGuesser/actions/workflows/dotnet.yml)  [![Coverage Status](https://coveralls.io/repos/github/HicServices/TypeGuesser/badge.svg?branch=master)](https://coveralls.io/github/HicServices/TypeGuesser?branch=master)  [![Total alerts](https://img.shields.io/lgtm/alerts/g/HicServices/TypeGuesser.svg?logo=lgtm&logoWidth=18)](https://lgtm.com/projects/g/HicServices/TypeGuesser/alerts/)  [![NuGet Badge](https://buildstats.info/nuget/HIC.TypeGuesser)](https://buildstats.info/nuget/HIC.TypeGuesser)
| [FAnsiSql](https://github.com/HicServices/FAnsiSql) | [![.NET Core](https://github.com/HicServices/FAnsiSql/actions/workflows/dotnet-core.yml/badge.svg)](https://github.com/HicServices/FAnsiSql/actions/workflows/dotnet-core.yml)  [![Total alerts](https://img.shields.io/lgtm/alerts/g/HicServices/FAnsiSql.svg?logo=lgtm&logoWidth=18)](https://lgtm.com/projects/g/HicServices/FAnsiSql/alerts/) [![NuGet Badge](https://buildstats.info/nuget/HIC.FAnsiSql)](https://www.nuget.org/packages/HIC.FansiSql/) |
| [DicomTypeTranslation](https://github.com/HicServices/DicomTypeTranslation) |  [![.NET Core](https://github.com/HicServices/DicomTypeTranslation/actions/workflows/dotnet-core.yml/badge.svg)](https://github.com/HicServices/DicomTypeTranslation/actions/workflows/dotnet-core.yml) [![Total alerts](https://img.shields.io/lgtm/alerts/g/HicServices/DicomTypeTranslation.svg?logo=lgtm&logoWidth=18)](https://lgtm.com/projects/g/HicServices/DicomTypeTranslation/alerts/) [![NuGet Badge](https://buildstats.info/nuget/HIC.DicomTypeTranslation)](https://buildstats.info/nuget/HIC.DicomTypeTranslation) |
| [RDMP](https://github.com/HicServices/RDMP) |   [![Build status](https://github.com/HicServices/RDMP/workflows/Build/badge.svg)](https://github.com/HicServices/RDMP/actions?query=workflow%3ABuild) [![Coverage Status](https://coveralls.io/repos/github/HicServices/RDMP/badge.svg?branch=develop)](https://coveralls.io/github/HicServices/RDMP?branch=develop) [![Total alerts](https://img.shields.io/lgtm/alerts/g/HicServices/RDMP.svg?logo=lgtm&logoWidth=18)](https://lgtm.com/projects/g/HicServices/RDMP/alerts/) [![NuGet Badge](https://buildstats.info/nuget/HIC.RDMP.Plugin)](https://buildstats.info/nuget/HIC.RDMP.Plugin) |
| [RDMP.Dicom](https://github.com/HicServices/RdmpDicom) | [![Build status](https://github.com/HicServices/RdmpDicom/actions/workflows/dotnet-core.yml/badge.svg)](https://github.com/HicServices/RdmpDicom/actions/workflows/dotnet-core.yml) [![Total alerts](https://img.shields.io/lgtm/alerts/g/HicServices/RdmpDicom.svg?logo=lgtm&logoWidth=18)](https://lgtm.com/projects/g/HicServices/RdmpDicom/alerts/) [![NuGet Badge](https://buildstats.info/nuget/HIC.RDMP.Dicom)](https://buildstats.info/nuget/HIC.RDMP.Dicom) |

[RabbitMQ]: https://www.rabbitmq.com/
[DBMS]: https://github.com/HicServices/RDMP/blob/develop/Documentation/CodeTutorials/Glossary.md#DBMS
[Dicom]: ./Glossary.md#dicom
[Dicom tags]: ./Glossary.md#dicom-tags
[IsIdentifiable]: ./src/microservices/Microservices.IsIdentifiable/README.md
[IsIdentifiableReviewer]: ./src/applications/Applications.IsIdentifiableReviewer/README.md
[DicomFileMessage]: ./src/common/Smi.Common/Messages/DicomFileMessage.cs
[SeriesMessage]: ./src/common/Smi.Common/Messages/SeriesMessage.cs
[ExtractionRequestMessage]: ./src/common/Smi.Common/Messages/Extraction/ExtractionRequestMessage.cs
[ExtractionRequestInfoMessage]: ./src/common/Smi.Common/Messages/Extraction/ExtractionRequestInfoMessage.cs
[ExtractFileMessage]: ./src/common/Smi.Common/Messages/Extraction/ExtractFileMessage.cs
[ExtractFileCollectionInfoMessage]: ./src/common/Smi.Common/Messages/Extraction/ExtractFileCollectionInfoMessage.cs
[ExtractedFileStatusMessage]: ./src/common/Smi.Common/Messages/Extraction/ExtractedFileStatusMessage.cs
[RDMP]: https://github.com/HicServices/RDMP
[ProcessDirectory]: ./src/applications/Applications.DicomDirectoryProcessor/README.md
[DicomTagReader]: ./src/microservices/Microservices.DicomTagReader/README.md
[IdentifierMapper]: ./src/microservices/Microservices.IdentifierMapper/Readme.md
[MongoDBPopulator]: ./src/microservices/Microservices.MongoDbPopulator/Readme.md
[DicomRelationalMapper]: ./src/microservices/Microservices.DicomRelationalMapper/Readme.md
[DicomReprocessor]: ./src/microservices/Microservices.DicomReprocessor/README.md
[ExtractImages]: ./src/applications/Applications.ExtractImages/README.md
[CohortExtractor]: ./src/microservices/Microservices.CohortExtractor/README.md
[CTPAnonymiser]: ./src/microservices/com.smi.microservices.ctpanonymiser/README.md
[CohortPackager]: ./src/microservices/Microservices.CohortPackager/README.md
[pre-commit]: https://pre-commit.com
