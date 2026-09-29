using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ICSharpCode.SharpZipLib.Zip;
using System.IO;
using System.Collections;

namespace Itim.TRS.InstallerLib
{
    public class Compression
    {
        static ArrayList _Files;

        public static void BuildList(string SearchPath, int RecursionLevel)
        {
            DirectoryInfo ThisLevel = new DirectoryInfo(SearchPath);
            DirectoryInfo[] ChildLevel = ThisLevel.GetDirectories();
            if (RecursionLevel != 1)
            {
                foreach (DirectoryInfo Child in ChildLevel)
                {
                    BuildList(Child.FullName, RecursionLevel - 1);
                }
            }
            FileInfo[] ChildFiles = ThisLevel.GetFiles();
            foreach (FileInfo ChildFile in ChildFiles)
            {
                _Files.Add(ChildFile.FullName);
            }
        }

        public static void ZipFolder(string basePath)
        {
            ZipFolder(basePath, false);
        }

        public static void ZipFolder(string basePath, bool deleteAfter)
        {
            string zipFileName = basePath + ".zip";
            ZipOutputStream os = new ZipOutputStream(File.OpenWrite(zipFileName));

            FileStream fs;

            _Files = new ArrayList();
            BuildList(basePath, 0);

            foreach (string s in _Files)
            {
                string sourceFileName = s;

                //string path = Path.GetFileName(sourceFileName);
                string path = Path.GetFullPath(sourceFileName).Replace(basePath, "");
                //string path1 = Path.GetExtension(path2);

                ZipEntry ze = new ZipEntry(path);
                ze.CompressionMethod = CompressionMethod.Deflated;
                os.PutNextEntry(ze);

                fs = File.OpenRead(sourceFileName);

                byte[] buff = new byte[1024];
                int n = 0;
                while ((n = fs.Read(buff, 0, buff.Length)) > 0)
                {
                    os.Write(buff, 0, n);

                }
                fs.Close();
            }

            os.CloseEntry();
            os.Close();

            if (deleteAfter)
            {
                Directory.Delete(basePath, true);
            }
        }

        public static void UnzipFolder(string zipFile, string baseFolder)
        {
            try
            {
                if (!Directory.Exists(baseFolder))
                {
                    Directory.CreateDirectory(baseFolder);
                }
                using (FileStream fr = File.OpenRead(zipFile))
                {
                    using (ZipInputStream ins = new ZipInputStream(fr))
                    {
                        ZipEntry ze = ins.GetNextEntry();

                        while (ze != null)
                        {
                            if (ze.IsDirectory)
                            {
                                Directory.CreateDirectory(Path.Combine(baseFolder, ze.Name));
                            }
                            else if (ze.IsFile)
                            {
                                if (!Directory.Exists(Path.Combine(baseFolder, Path.GetDirectoryName(ze.Name))))
                                {
                                    Directory.CreateDirectory(Path.Combine(baseFolder, Path.GetDirectoryName(ze.Name)));
                                }

                                using (FileStream fs = File.Create(Path.Combine(baseFolder, ze.Name)))
                                {

                                    byte[] writeData = new byte[ze.Size];
                                    int iteration = 0;
                                    while (true)
                                    {
                                        int size = 2048;
                                        size = ins.Read(writeData, (int)Math.Min(ze.Size, (iteration * 2048)), (int)Math.Min(ze.Size - (int)Math.Min(ze.Size, (iteration * 2048)), 2048));
                                        if (size > 0)
                                        {
                                            fs.Write(writeData, (int)Math.Min(ze.Size, (iteration * 2048)), size);
                                        }
                                        else
                                        {
                                            break;
                                        }
                                        iteration++;
                                    }
                                    fs.Close();
                                }
                            }
                            ze = ins.GetNextEntry();
                        }
                        ins.Close();
                    }
                    fr.Close();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Unzip failed with error: " + ex.Message, ex);
            }
        }
    }
}
