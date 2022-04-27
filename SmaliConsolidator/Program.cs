using System;

namespace SmaliConsolidator
{
    internal class Program
    {
        static void Main(string[] args)
        {
            List<string> files = GetFolderFiles();

            ConsolidateFiles(files);
        }

        private static List<string> GetFolderFiles()
        {
            Console.WriteLine("Please enter the path and folder name to read from:");
            string folderPath = Console.ReadLine();

            if(!Directory.Exists(folderPath))
            {
                Console.WriteLine(folderPath + " does not exist!");
                throw new DirectoryNotFoundException(folderPath);
            }

            List<string> foldersToCheck = new List<string>();
            foldersToCheck.Add(folderPath);
            List<string> returnList = new List<string>();
            returnList.Add(folderPath);
            while(true)
            {
                string folderToCheck = foldersToCheck[0];
                foldersToCheck.RemoveAt(0);
                string[] files = Directory.GetFiles(folderToCheck);
                foreach(string file in files)
                {
                    returnList.Add(file);
                }

                string[] subDirs = Directory.GetDirectories(folderToCheck);
                if (subDirs.Length > 0)
                {
                    foreach (string subDir in subDirs)
                    {
                        foldersToCheck.Add(subDir);
                    }
                }
                else
                {
                    break;
                }
            }

            Console.WriteLine(returnList.Count + " files found!");

            return returnList;
        }

        private static void ConsolidateFiles(List<string> files)
        {
            Console.WriteLine("Please enter the path and folder name to write to:");
            string targetFolder = Console.ReadLine();

            if (!Directory.Exists(targetFolder))
            {
                Console.WriteLine(targetFolder + " does not exist!");
                throw new DirectoryNotFoundException(targetFolder);
            }

            bool skip = true;
            foreach (string file in files)
            {
                if (skip)
                {
                    skip = false;
                    continue;
                }

                string relativeFile = targetFolder + file.Replace(files[0], "");
                if (File.Exists(relativeFile))
                {
                    List<string> lineIds = new List<string>();
                    List<string> lineTypes = new List<string>();
                    foreach (string line in File.ReadLines(relativeFile))
                    {
                        if(line.Length > 0 && line[0] == '.')
                        {
                            string lineType = line.Substring(0, line.IndexOf(" "));
                            if(lineType != ".end")
                            {
                                string lineId = line.Substring(line.LastIndexOf(" "));
                                lineIds.Add(lineId);
                                lineTypes.Add(lineType);
                            }
                        }
                    }

                    bool newLines = false;

                    int lineCount = 0;
                    string toWrite = "\n";
                    foreach (string line in File.ReadLines(file))
                    {
                        if (line.Length > 0)
                        {
                            if (line[0] == '.')
                            {
                                string lineId = line.Substring(line.LastIndexOf(" "));
                                string lineType = line.Substring(0, line.IndexOf(" "));
                                int index = lineIds.IndexOf(lineId);
                                if (lineType == ".end" && newLines)
                                {
                                    toWrite += line + "\n\n";
                                    lineCount += 1;
                                }
                                else if (lineType != ".end" && (index < 0 || lineTypes[index] != lineType))
                                {
                                    newLines = true;
                                    toWrite += line + "\n\n";
                                    lineCount += 1;
                                }
                                else
                                {
                                    newLines = false;
                                }
                            }
                            else if (line[0] == ' ' && newLines)
                            {
                                toWrite += line + "\n\n";
                                lineCount += 1;
                            }
                        }
                    }

                    File.AppendAllText(relativeFile, toWrite);

                    Console.WriteLine();
                    Console.WriteLine(relativeFile);
                    Console.WriteLine(lineCount + " lines added! (excluding whitelines)");
                }
                else
                {
                    string targetDirectory = relativeFile.Substring(0, relativeFile.LastIndexOf('\\'));
                    if (!Directory.Exists(targetDirectory))
                    {
                        Directory.CreateDirectory(targetDirectory);
                    }

                    File.Copy(file, relativeFile);

                    Console.WriteLine();
                    Console.WriteLine(relativeFile);
                    Console.WriteLine("File copied!");
                }
            }
        }
    }
}