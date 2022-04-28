using System;
using System.Text;

namespace SmaliConsolidator
{
    internal class Program
    {
        private static List<string> possibleRegisters;

        static void Main(string[] args)
        {
            possibleRegisters = GeneratePossibleRegisters(256);
            List<string> files = GetFolderFiles();

            ConsolidateFiles(files);
        }

        private static List<string> GeneratePossibleRegisters(int maxRegisters)
        {
            List<string> returnList = new List<string>();
            for(int i = 0; i < maxRegisters; i++)
            {
                returnList.Add("v" + i);
            }
            return returnList;
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
            foreach (string readFromFilePath in files)
            {
                if (skip)
                {
                    skip = false;
                    continue;
                }

                string writeToFilePath = targetFolder + readFromFilePath.Replace(files[0], "");
                if (File.Exists(writeToFilePath))
                {
                    List<Element> writeToElements = GetElementsFromFile(writeToFilePath);
                    List<Element> readFromElements = GetElementsFromFile(readFromFilePath);

                    // Setup static variable to replace readFromFile .super
                    bool mixedSuper = false;
                    string currentObjectType = "";
                    string objectType = "";
                    string staticVarName = "";
                    Element? writeToSuper = writeToElements.Find(x => x.Type == "super");
                    Element? readFromSuper = readFromElements.Find(x => x.Type == "super");
                    if(writeToSuper != null && readFromSuper != null && writeToSuper.Identifier != readFromSuper.Identifier)
                    {
                        mixedSuper = true;

                        List<string> variableNames = new List<string>();
                        variableNames.AddRange(GetVariableNamesFromElements(readFromElements));
                        variableNames.AddRange(GetVariableNamesFromElements(writeToElements));

                        while (true)
                        {
                            staticVarName = GenerateRandomString(5);
                            if(!variableNames.Contains(staticVarName))
                            {
                                break;
                            }
                        }

                        // Add static variable field
                        objectType = readFromSuper.Identifier.Substring(readFromSuper.Identifier.IndexOf("L"), readFromSuper.Identifier.LastIndexOf(";"));
                        Element staticElement = new Element(".field private static " + staticVarName + ":" + objectType);

                        Element? clinit = writeToElements.Find(x => x.Type == "method" && x.Identifier.Contains("<clinit>"));
                        if(clinit == null)
                        {
                            clinit = readFromElements.Find(x => x.Type == "method" && x.Identifier.Contains("<clinit>"));
                            if(clinit != null)
                            {
                                readFromElements.Remove(clinit);
                            }
                        }
                        else
                        {
                            writeToElements.Remove(clinit);
                        }

                        Element? classElement = writeToElements.Find(x => x.Type == "class");
                        if (classElement != null)
                        {
                            // TODO - One more problem with the <init>
                            // Some <init> functions have an ObjectType defined that it returns. Some don't
                            // Possible fixes:
                            // - Go to the actual file of the class and check the <init> function there
                            // - Look for the similar <init> in the same file (PICK THIS ONE)
                            // It is always because another type is returned than the actual type (like MyRepository returning an Executor)

                            currentObjectType = classElement.Identifier.Trim();
                            if (clinit == null)
                            {
                                clinit = new Element(".method static constructor <clinit>()V");
                                clinit.AddLine("    .locals 1");
                                clinit.AddLine("    new-instance v0, " + objectType);
                                clinit.AddLine("    invoke-direct {v0}, " + objectType + "-><init>()V");
                                clinit.AddLine("    sput-object v0, " + currentObjectType + "->" + staticVarName + ":" + objectType);
                                clinit.AddLine(".end method");
                            }
                            else
                            {
                                // TODO - Change already existing .locals here
                                string? localsLine = clinit.GetLines().Find(x => x.Contains(".locals"));
                                if(localsLine != null)
                                {
                                    int localsCount = Convert.ToInt32(localsLine.Trim().Substring(8));
                                    localsCount += 1;
                                    string newLocalsLine = "    .locals " + localsCount;
                                    clinit.ReplaceLine(localsLine, newLocalsLine);
                                }

                                clinit.AddLine("    new-instance v0, " + objectType);
                                clinit.AddLine("    invoke-direct {v0}, " + objectType + "-><init>()V");
                                clinit.AddLine("    sput-object v0, " + currentObjectType + "->" + staticVarName + ":" + objectType);
                            }

                            writeToElements.Add(clinit);
                        }
                    }

                    // Add elements from readFromFile to writeToFile
                    foreach (Element element in readFromElements)
                    {
                        if(element.Type != "super" && element.Type != "class" && element.Type != "source" && writeToElements.Find(x => x.Type == element.Type && x.Identifier == element.Identifier) == null)
                        {
                            if(element.Type == "method" && mixedSuper)
                            {
                                List<string>? usedRegisters = null;
                                foreach (string line in element.GetLines())
                                {
                                    string trimLine = line.Trim();
                                    int spaceIndex = trimLine.IndexOf(" ");
                                    if (spaceIndex >= 0)
                                    {
                                        string opCode = trimLine.Substring(0, spaceIndex).Trim();
                                        if(opCode == "invoke-direct" || opCode == "invoke-virtual")
                                        {
                                            string objectUsed = trimLine.Substring(spaceIndex);
                                            if(objectUsed.Contains(objectType))
                                            {
                                                // TODO - Check if the function is <init. If it is, just remove it and don't replace. The <init> now exists in the <clinit> already

                                                // Get all the registers used in the method, but only once for performance sake.
                                                if (usedRegisters == null)
                                                {
                                                    usedRegisters = new List<string>();
                                                    foreach (string registerLine in element.GetLines())
                                                    {
                                                        string registerTrimLine = registerLine.Trim();
                                                        if (registerTrimLine.Length > 0 && registerTrimLine[0] != '.')
                                                        {
                                                            int registerSpaceIndex = registerTrimLine.IndexOf(" ");
                                                            if (registerSpaceIndex >= 0)
                                                            {
                                                                string register = registerTrimLine.Substring(registerSpaceIndex).Trim();
                                                                int registerIndex = register.IndexOf(" ");
                                                                if (registerIndex >= 0)
                                                                {
                                                                    register = register.Substring(0, register.IndexOf(" "));
                                                                    register = RemoveSpecialCharacters(register);
                                                                    usedRegisters.Add(register);
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                                string? availableRegister = possibleRegisters.Find(x => !usedRegisters.Contains(x));

                                                string? localsLine = element.GetLines().Find(x => x.Contains(".locals"));
                                                if (localsLine != null)
                                                {
                                                    int localsCount = Convert.ToInt32(localsLine.Trim().Substring(8));
                                                    localsCount += 1;
                                                    string newLocalsLine = "    .locals " + localsCount;
                                                    element.ReplaceLine(localsLine, newLocalsLine);
                                                }

                                                string getObjectLine = "    sget-object " + availableRegister + ", " + currentObjectType + "->" + staticVarName + ":" + objectType;
                                                string invokeLine = "    " + opCode + " {" + availableRegister;

                                                string registersUsedStr = objectUsed.Substring(objectUsed.IndexOf("{"), objectUsed.LastIndexOf("}"));
                                                string[] registersUsed = registersUsedStr.Split(new char[] { ',' });
                                                if(registersUsed.Length > 1)
                                                {
                                                    // Copy registers, except for the first one
                                                    bool skipRegister = true;
                                                    foreach(string register in registersUsed)
                                                    {
                                                        if(skipRegister)
                                                        {
                                                            skipRegister = false;
                                                            continue;
                                                        }

                                                        invokeLine += ", " + register;
                                                    }
                                                }
                                                invokeLine += "}, " + objectType + "->";

                                                string functionCalled = objectUsed.Substring(objectUsed.IndexOf("<"));
                                                invokeLine += functionCalled;

                                                element.ReplaceLines(line, new string[] { getObjectLine, invokeLine });
                                            }
                                        }
                                    }
                                }
                            }

                            writeToElements.Add(element);
                        }
                    }

                    foreach(Element element in writeToElements)
                    {
                        //File.WriteAllLines(writeToFilePath, element.GetLines());
                    }
                }
                else
                {
                    /*string targetDirectory = relativeFile.Substring(0, relativeFile.LastIndexOf('\\'));
                    if (!Directory.Exists(targetDirectory))
                    {
                        Directory.CreateDirectory(targetDirectory);
                    }

                    File.Copy(file, relativeFile);

                    Console.WriteLine();
                    Console.WriteLine(relativeFile);
                    Console.WriteLine("File copied!");*/
                }
            }
        }

        private static string RemoveSpecialCharacters(string str)
        {
            StringBuilder sr = new StringBuilder(str.Length);
            foreach (char c in str)
            {
                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
                {
                    sr.Append(c);
                }
            }
            return sr.ToString();
        }

        private static string GenerateRandomString(int length)
        {
            StringBuilder sr = new StringBuilder();
            Random random = new Random();

            for (int i = 0; i < length; i++)
            {
                double flt = random.NextDouble();
                int shift = Convert.ToInt32(Math.Floor(25 * flt));
                char letter = Convert.ToChar(shift + 65);
                sr.Append(letter);
            }

            return sr.ToString();
        }

        private static List<Element> GetElementsFromFile(string filePath)
        {
            List<Element> returnList = new List<Element>();
            Element? addElement = null;
            foreach (string line in File.ReadLines(filePath))
            {
                if (line.Length > 0)
                {
                    if (line[0] == '.' && !line.Contains(".end"))
                    {
                        addElement = new Element(line);
                        returnList.Add(addElement);
                    }
                    else if (addElement != null && line[0] != '#')
                    {
                        addElement.AddLine(line);
                    }
                }
            }

            return returnList;
        }

        private static List<string> GetVariableNamesFromElements(List<Element> elements)
        {
            List<string> returnList = new List<string>();
            foreach (Element fieldEle in elements.FindAll(x => x.Type == "field"))
            {
                string identifier = fieldEle.Identifier;
                string varName = identifier.Substring(0, identifier.IndexOf(':'));
                returnList.Add(varName);
            }
            return returnList;
        }
    }
}