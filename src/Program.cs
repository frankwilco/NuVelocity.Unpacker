using SixLabors.ImageSharp.Formats.Tga;
using System.IO;
using NuVelocity.IO;
using System.Diagnostics;

namespace NuVelocity.Unpacker
{
    internal class Program
    {
        /*
        static void TestSequenceRepack()
        {
            Sequence.Repack(
                File.OpenWrite("repack.Sequence"),
                File.ReadAllBytes("seq1"),
                File.ReadAllBytes("seq2"),
                File.ReadAllBytes("seq3"));
        }

        
        public static void Repack(Stream stream, byte[] frameInfo, byte[] spritesheet, byte[] mask)
        {
            BinaryWriter writer = new(stream);
            writer.Write(kSignatureStandard);

            Deflater deflater = new(Deflater.BEST_COMPRESSION);

            byte[] bufferFrameInfo = new byte[frameInfo.Length * 2];
            byte[] bufferMask = new byte[mask.Length * 2];

            deflater.SetInput(frameInfo);
            deflater.Flush();
            int deflatedSize = deflater.Deflate(bufferFrameInfo);
            deflater.Finish();
            writer.Write(deflatedSize);
            writer.Write(frameInfo.Length);
            writer.Write(bufferFrameInfo, 0, deflatedSize);

            writer.Write(false);
            writer.Write(spritesheet.Length);
            writer.Write(spritesheet);
            writer.Write((byte)0x0);

            writer.Write(mask.Length);
            deflater.Reset();
            deflater.SetInput(mask);
            deflater.Flush();
            deflatedSize = deflater.Deflate(bufferMask);
            deflater.Finish();
            writer.Write(bufferMask, 0, deflatedSize);

            writer.Flush();
        }
        */
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
                File.WriteAllText($"{target}\\Properties.txt", b.RawList.Serialize());
                File.WriteAllBytes($"{target}\\_lists", b._embeddedLists);
                if (b._sequenceSpriteSheet != null)
                {
                    File.WriteAllBytes($"{target}\\_rawImage", b._sequenceSpriteSheet);
                }
                if (b._rawMaskData != null)
                {
                    File.WriteAllBytes($"{target}\\_rawMask", b._rawMaskData);
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