// <copyright file="Obfuscation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Threading;
using Aspose.Pdf;

namespace Samples.Computer01
{
    internal class Obfuscation : ScenarioBase
    {
        public override void OnProcess()
        {
            try
            {
                Document pdf_doc = new Document();
                Page page = pdf_doc.Pages.Add();
                page.Paragraphs.Add(new Aspose.Pdf.Text.TextFragment("Line1"));
                page.Paragraphs.Add(new Aspose.Pdf.Text.TextFragment("Line2"));
                page.Paragraphs.Add(new Aspose.Pdf.Text.TextFragment("Line3"));
                page.Paragraphs.Add(new Aspose.Pdf.Text.TextFragment("Line4"));
                page.Paragraphs.Add(new Aspose.Pdf.Text.TextFragment("Line5"));
                page.Paragraphs.Add(new Aspose.Pdf.Text.TextFragment("Line6"));

                var filename = Path.GetTempFileName();
                Console.WriteLine(filename);
                pdf_doc.Save(filename);
                File.Delete(filename);
            }
            catch (Exception x)
            {
                Console.WriteLine(x.Message);
            }
        }

        public override void Run()
        {
            Start();
            Thread.Sleep(TimeSpan.FromMilliseconds(100));
            Stop();
        }
    }
}
