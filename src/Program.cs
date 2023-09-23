using SixLabors.ImageSharp.Formats.Tga;
using System.IO;
using NuVelocity.IO;
using System.Diagnostics;

namespace NuVelocity.Unpacker
{
    internal class Program
    {
        static TgaEncoder tgaEncoder => new()
        {
            BitsPerPixel = TgaBitsPerPixel.Pixel32,
            Compression = TgaCompression.RunLength
        };

        static void TestFrame()
        {
            foreach (string file in Directory.EnumerateFiles("Tests", "*.Frame", SearchOption.AllDirectories))
            {
                Frame frame = new(File.Open(file, FileMode.Open));
                string target = file.Replace(".Frame", ".");
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                var rawData = frame.DumpRawData();
                if (rawData.Item1 != null)
                {
                    File.WriteAllBytes($"{target}_data", rawData.Item1);
                }
                if (rawData.Item2 != null)
                {
                    File.WriteAllBytes($"{target}_rawMask", rawData.Item2);
                }
                frame.ToImage().Save(target + "tga", tgaEncoder);
                frame.ToImage().SaveAsPng(target + "png");
                string logText = $"{file} " +
                    $": {BitConverter.ToString(BitConverter.GetBytes(frame.Offset.X))} " +
                    $": {BitConverter.ToString(BitConverter.GetBytes(frame.Offset.Y))}\n";
                Console.Write(logText);
            }
        }

        static void TestSequence()
        {
            foreach (string file in Directory.EnumerateFiles("Tests", "*.Sequence", SearchOption.AllDirectories))
            {
                var b = new Sequence(File.Open(file, FileMode.Open));
                string sequenceName = Path.GetFileNameWithoutExtension(file);
                string target = Path.Combine(Path.GetDirectoryName(file), "-" + sequenceName);
                Directory.CreateDirectory(target);
                if (b.RawList != null)
                {
                    File.WriteAllText($"{target}\\Properties.txt", b.RawList.Serialize());
                }
                var rawData = b.DumpRawData();
                if (rawData.Item1 != null)
                {
                    File.WriteAllBytes($"{target}\\_lists", rawData.Item1);
                }
                if (rawData.Item2 != null)
                {
                    File.WriteAllBytes($"{target}\\_rawImage", rawData.Item2);
                }
                if (rawData.Item3 != null)
                {
                    File.WriteAllBytes($"{target}\\_rawMask", rawData.Item3);
                }

                var bo = b.ToImage();
                if (bo != null)
                {
                    bo.SaveAsPng($"{target}\\_atlas.png");
                }

                string sequenceSimpleName = sequenceName.Replace(" ", "");
                var images = b.ToImages();
                for (int i = 0; i < images.Length; i++)
                {
                    images[i].Save($"{target}\\{sequenceSimpleName}{i:0000}.tga", tgaEncoder);
                    images[i].SaveAsPng($"{target}\\{sequenceSimpleName}{i:0000}.png");
                }
                Console.WriteLine(file);
            }
        }

        static void Main(string[] args)
        {
            //TestFrame();
            //TestSequence();
            //TestSequenceRepack();
            //return;

            if (File.Exists("log.txt"))
            {
                File.Delete("log.txt");
            }

            foreach (string file in Directory.EnumerateFiles("Data", "*.Frame", SearchOption.AllDirectories))
            {
                Frame frame = new(File.Open(file, FileMode.Open));
                string target = file.Replace("\\Cache", "\\Export").Replace(".Frame", ".tga");
                Directory.CreateDirectory(Path.GetDirectoryName(target));

                string propTarget = file.Replace("\\Cache", "").Replace(".Frame", ".txt");
                if (File.Exists(propTarget))
                {
                    frame.ReadPropertiesFromStream(File.Open(propTarget, FileMode.Open));
                }
                frame.ToImage().Save(target, tgaEncoder);

                bool? centerHotSpot = null;
                if (frame.RawList != null)
                {
                    RawProperty centerHotSpotProp = frame.RawList.Properties
                        .FirstOrDefault((property) => property.Name == "Center Hot Spot", null);
                    centerHotSpot = centerHotSpotProp == null
                        ? false
                        : ((string)centerHotSpotProp.Value) == "1";
                }

                string logText = $"{file} " +
                    $": {BitConverter.ToString(BitConverter.GetBytes(frame.Offset.X))} " +
                    $": {BitConverter.ToString(BitConverter.GetBytes(frame.Offset.Y))} " +
                    $": {centerHotSpot}\n";
                File.AppendAllText("log.txt", logText);
                Console.Write(logText);
            }

            foreach (string file in Directory.EnumerateFiles("Data", "*.Sequence", SearchOption.AllDirectories))
            {
                Sequence sequence = new(File.Open(file, FileMode.Open));
                string sequenceName = Path.GetFileNameWithoutExtension(file);
                string sequenceSimpleName = sequenceName.Replace(" ", "");
                string target = Path.Combine(Path.GetDirectoryName(file).Replace("\\Cache", "\\Export"), "-" + sequenceName);
                Directory.CreateDirectory(target);
                var images = sequence.ToImages();
                bool? centerHotSpot = null;
                if (sequence.RawList != null)
                {
                    File.WriteAllText($"{target}\\Properties.txt", sequence.RawList.Serialize());
                    centerHotSpot = ((string)sequence.RawList.Properties
                        .First((property) => property.Name == "Center Hot Spot").Value) == "1";
                }
                if (images == null)
                {
                    continue;
                }
                for (int i = 0; i < images.Length; i++)
                {
                    var image = images[i];
                    image.Save($"{target}\\{sequenceSimpleName}{i:0000}.tga", tgaEncoder);
                }
                string logText = $"{file} : {centerHotSpot}\n";
                File.AppendAllText("log.txt", logText);
                Console.Write(logText);
            }

            Console.WriteLine("Done.");
        }
    }
}