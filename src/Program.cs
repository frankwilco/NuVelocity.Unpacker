using SixLabors.ImageSharp.Formats.Tga;
using System.IO;
using NuVelocity.IO;

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
            var a = new Frame(File.Open("test.Frame", FileMode.Open));
            a.ToImage().SaveAsPng("test.png");
            a.ToImage().Save("test.tga", tgaEncoder);
            File.WriteAllBytes("test.b", a.ToBytes());
        }

        static void TestSequence()
        {
            var b = new Sequence(File.Open("test.Sequence", FileMode.Open));
            File.WriteAllText("Properties.txt", b.RawList.Serialize());
            //File.WriteAllBytes("seq1", b._embeddedLists);
            //File.WriteAllBytes("seq2", b._sequenceSpriteSheet);
            //if (b._rawMaskData != null)
            //{
            //    File.WriteAllBytes("seq3", b._rawMaskData);
            //}

            var bo = b.ToImage();
            if (bo != null)
            {
                bo.SaveAsPng("testseq.png");
            }

            var images = b.ToImages();
            for (int i = 0; i < images.Length; i++)
            {
                images[i].SaveAsPng($"testseq{i}.png");
            }
        }

        static void Main(string[] args)
        {
            //TestFrame();
            //TestSequence();
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
                frame.ToImage().Save(target, tgaEncoder);
                File.AppendAllText("log.txt", $"{file} " +
                    $": {BitConverter.ToString(frame.Unknown1)} " +
                    $": {BitConverter.ToString(frame.Unknown2)}\n");
            }

            foreach (string file in Directory.EnumerateFiles("Data", "*.Sequence", SearchOption.AllDirectories))
            {
                Sequence sequence = new(File.Open(file, FileMode.Open));
                string sequenceName = Path.GetFileNameWithoutExtension(file);
                string sequenceSimpleName = sequenceName.Replace(" ", "");
                string target = Path.Combine(Path.GetDirectoryName(file).Replace("\\Cache", "\\Export"), "-" + sequenceName);
                Directory.CreateDirectory(target);
                File.WriteAllText($"{target}\\Properties.txt", sequence.RawList.Serialize());
                var images = sequence.ToImages();
                if (images == null)
                {
                    continue;
                }
                for (int i = 0; i < images.Length; i++)
                {
                    var image = images[i];
                    image.Save($"{target}\\{sequenceSimpleName}{i:0000}.tga", tgaEncoder);
                }
                File.AppendAllText("log.txt", $"{file}\n");
            }

            Console.WriteLine("Done.");
        }
    }
}