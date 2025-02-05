﻿
using FellowOakDicom;
using DicomTypeTranslation;
using Microservices.DicomRelationalMapper.Execution;
using NUnit.Framework;
using Rdmp.Core.DataFlowPipeline;
using Rdmp.Dicom.PipelineComponents.DicomSources;
using ReusableLibraryCode.Progress;
using System.IO;

namespace Microservices.Tests.RDMPTests
{
    public class AutoRoutingAttacherTests
    {

        [Test]
        public void TestPatientAgeTag()
        {
            string filename = Path.Combine(TestContext.CurrentContext.TestDirectory, "test.dcm");

            var dataset = new DicomDataset();
            dataset.Add(DicomTag.SOPInstanceUID, "123.123.123");
            dataset.Add(DicomTag.SOPClassUID, "123.123.123");
            dataset.Add(new DicomAgeString(DicomTag.PatientAge, "009Y"));

            var cSharpValue = DicomTypeTranslaterReader.GetCSharpValue(dataset, DicomTag.PatientAge);

            Assert.AreEqual("009Y", cSharpValue);


            var file = new DicomFile(dataset);
            file.Save(filename);


            var source = new DicomFileCollectionSource();
            source.FilenameField = "Path";
            source.PreInitialize(new ExplicitListDicomFileWorklist(new[] { filename }), new ThrowImmediatelyDataLoadEventListener());


            var chunk = source.GetChunk(new ThrowImmediatelyDataLoadEventListener(), new GracefulCancellationToken());

            Assert.AreEqual("009Y", chunk.Rows[0]["PatientAge"]);
        }
    }
}
