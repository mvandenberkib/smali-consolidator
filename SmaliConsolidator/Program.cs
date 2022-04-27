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

                // Get all the relevant data in the file to write to
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

                    // Go through the file to read from and if a line that isn't in the other file is found, mark it to be added to the file to write to
                    int lineCount = 0;
                    string toWrite = "\n";
                    foreach (string line in File.ReadLines(file))
                    {
                        if (line.Length > 0)
                        {
                            if (line[0] == '.')
                            {
                                /* Check for a .super and if it's found check if it's different than the one in the lineIds
                                   If it's different store that value and replace any references to it with the right calls (if it's the same just ignore it)
                                   Also, if it's found, you need to add the initialization code for the static variable to the <clinit> code
                                   This way it is guaranteed to be initialized when needed to be used later

                                    Put this in <clinit>:
                                    .field private static <varName>:ObjectType
                                    new-instance <register>, ObjectType
                                    sput-object <register>, ThisObjectType;-><varName>

                                    Make sure the chosen varName isn't already taken by another variable. Use the lineIds and lineTypes (.field) to check this
                                    Also, this applies to any register use anywhere, but make sure the register used is not already being used. Otherwise values may be overwritten that need to be used later
                                    Keep a list of registers while going through a function. Keep a constant list of registers that can be used. When needing a register pick the first register in the constant list that isn't in the other list
                                    
                                    Replace any function calls to the previous super with the sget and either the invoke-direct or the invoke-virtual
                                    invoke-direct can be replaced with invoke-direct and invoke-virtual can be replaced with invoke-virtual
                                    invoke-static doesn't need to be replaced
                                    The reason for this is that invoke-direct is used for constructors (or private functions but that's not relevant for us as you can't use private functions when inheriting anyway)
                                    invoke-static is used exclusively for static functions (which don't need a reference to the static object we initialized so we can keep it as is)
                                    and invoke-virtual is used for all others, so these all need to be replaced as the inherited public functions aren't accessible anymore

                                    sget-object <register>, ThisObjectType;-><varName>
                                    invoke-direct {<register>}, ObjectType;->Function
                                    invoke-virtual {<register>}, ObjectType;->Function

                                    If there is no <clinit>, add it. It may not always be present.
                                    .method static constructor <clinit>()V
                                        Initialize static stuff
                                        return-void
                                    .end method
                                 */

                                string lineId = line.Substring(line.LastIndexOf(" "));
                                string lineType = line.Substring(0, line.IndexOf(" "));
                                int index = lineIds.IndexOf(lineId);
                                if(lineType == ".source" || lineType == ".class")
                                {
                                    continue;
                                }

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

                    if (lineCount > 0)
                    {
                        Console.WriteLine();
                        Console.WriteLine(relativeFile);
                        Console.WriteLine(lineCount + " lines added! (excluding whitelines)");
                    }
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