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
            for (int i = 0; i < maxRegisters; i++)
            {
                returnList.Add("v" + i);
            }
            return returnList;
        }

        private static List<string> GetFolderFiles()
        {
            Console.WriteLine("Please enter the path and folder name to read from:");
            string folderPath = Console.ReadLine();

            if (!Directory.Exists(folderPath))
            {
                Console.WriteLine(folderPath + " does not exist!");
                throw new DirectoryNotFoundException(folderPath);
            }

            List<string> foldersToCheck = new List<string>();
            foldersToCheck.Add(folderPath);
            List<string> returnList = new List<string>();
            returnList.Add(folderPath);
            while (true)
            {
                string folderToCheck = foldersToCheck[0];
                foldersToCheck.RemoveAt(0);
                string[] files = Directory.GetFiles(folderToCheck);
                foreach (string file in files)
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
                // Skip the first entry because it is the root folder, not a file
                if (skip)
                {
                    skip = false;
                    continue;
                }

                string writeToFilePath = targetFolder + readFromFilePath.Replace(files[0], "");

                // If the file exists already, go into it to edit it
                // Otherwise just copy it
                if (File.Exists(writeToFilePath))
                {
                    EditFile(writeToFilePath, readFromFilePath);
                }
                else
                {
                    string targetDirectory = writeToFilePath.Substring(0, writeToFilePath.LastIndexOf('\\'));
                    if (!Directory.Exists(targetDirectory))
                    {
                        Directory.CreateDirectory(targetDirectory);
                    }

                    File.Copy(readFromFilePath, writeToFilePath);

                    Console.WriteLine();
                    Console.WriteLine(writeToFilePath);
                    Console.WriteLine("File copied!");
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

        private static void SetupStaticVar(List<Element> writeToElements, List<Element> readFromElements, out string? currentObjectType, out string? objectType, out string? staticVarName)
        {
            currentObjectType = null;
            objectType = null;
            staticVarName = null;

            Element? readFromSuper = readFromElements.Find(x => x.Type == "super");

            if (readFromSuper != null)
            {
                List<string> variableNames = new List<string>();
                variableNames.AddRange(GetVariableNamesFromElements(readFromElements));
                variableNames.AddRange(GetVariableNamesFromElements(writeToElements));

                while (true)
                {
                    staticVarName = GenerateRandomString(5);
                    if (!variableNames.Contains(staticVarName))
                    {
                        break;
                    }
                }

                // Add static variable field
                objectType = readFromSuper.Identifier.Substring(readFromSuper.Identifier.IndexOf("L"), readFromSuper.Identifier.LastIndexOf(";"));
                Element staticElement = new Element(".field private static " + staticVarName + ":" + objectType);
                writeToElements.Add(staticElement);

                Element? classElement = writeToElements.Find(x => x.Type == "class");
                if (classElement != null)
                {
                    currentObjectType = classElement.Identifier.Trim();
                }
            }
        }

        private static List<string> GetUsedRegisters(Element methodElement)
        {
            List<string> returnList = new List<string>();
            foreach (string registerLine in methodElement.GetLines())
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
                            returnList.Add(register);
                        }
                    }
                }
            }
            return returnList;
        }

        private static string[] CreateMethodLines(string availableRegister, string currentObjectType, string objectUsed, string objectType, string opCode, string staticVarName)
        {
            string getObjectLine = "    sget-object " + availableRegister + ", " + currentObjectType + "->" + staticVarName + ":" + objectType;
            string invokeLine = "    " + opCode + " {" + availableRegister;

            string registersUsedStr = objectUsed.Substring(objectUsed.IndexOf("{"), objectUsed.LastIndexOf("}"));
            string[] registersUsed = registersUsedStr.Split(new char[] { ',' });
            if (registersUsed.Length > 1)
            {
                // Copy registers, except for the first one
                bool skipRegister = true;
                foreach (string register in registersUsed)
                {
                    if (skipRegister)
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

            return new string[] { getObjectLine, invokeLine };
        }

        private static string[] CreateInitLines(string availableRegister, string currentObjectType, string objectUsed, string objectType, string staticVarName)
        {
            int functionIndex = objectUsed.IndexOf("->");
            string function = objectUsed.Substring(functionIndex + 2);
            string newInstanceLine = "    new-instance " + availableRegister + ", " + objectType;

            string invokeLine = "    invoke-direct {" + availableRegister;
            string registersUsedStr = objectUsed.Substring(objectUsed.IndexOf("{") + 1, objectUsed.LastIndexOf("}") - 2);
            string[] registersUsed = registersUsedStr.Split(new char[] { ',' });
            if (registersUsed.Length > 1)
            {
                // Copy registers, except for the first one
                bool skipRegister = true;
                foreach (string register in registersUsed)
                {
                    if (skipRegister)
                    {
                        skipRegister = false;
                        continue;
                    }

                    invokeLine += ", " + register.Trim();
                }
            }
            invokeLine += "}, " + objectType + "->";
            string functionCalled = objectUsed.Substring(objectUsed.IndexOf("<"));
            invokeLine += functionCalled;

            string sputLine = "    sput-object " + availableRegister + ", " + currentObjectType + "->" + staticVarName + ":" + objectType;

            return new string[] { newInstanceLine, invokeLine, sputLine };
        }

        private static void EditFile(string writeToFilePath, string readFromFilePath)
        {
            List<Element> writeToElements = GetElementsFromFile(writeToFilePath);
            List<Element> readFromElements = GetElementsFromFile(readFromFilePath);

            // Setup static variable to replace readFromFile .super
            Element? writeToSuper = writeToElements.Find(x => x.Type == "super");
            Element? readFromSuper = readFromElements.Find(x => x.Type == "super");
            bool mixedSuper = writeToSuper != null && readFromSuper != null && writeToSuper.Identifier != readFromSuper.Identifier;

            string? staticVarName = null;

            // Add elements from readFromFile to writeToFile
            foreach (Element element in readFromElements)
            {
                if (((mixedSuper && element.Type != "super") || !mixedSuper) && element.Type != "class" && element.Type != "source" && writeToElements.Find(x => x.Type == element.Type && x.Identifier == element.Identifier) == null)
                {
                    // Edit the .method accordingly
                    if (element.Type == "method" && mixedSuper)
                    {
                        List<string>? usedRegisters = null;
                        string? currentObjectType = null;
                        string? objectType = null;
                        bool overwriteLocalRegister = false;
                        string? localRegisterToOverwrite = null;
                        string? availableRegister = null;
                        foreach (string line in element.GetLines())
                        {
                            string trimLine = line.Trim();
                            int spaceIndex = trimLine.IndexOf(" ");
                            if (spaceIndex >= 0)
                            {
                                string opCode = trimLine.Substring(0, spaceIndex).Trim();
                                if (opCode.Contains("invoke"))
                                {
                                    if (staticVarName == null)
                                    {
                                        SetupStaticVar(writeToElements, readFromElements, out currentObjectType, out objectType, out staticVarName);
                                    }

                                    if (currentObjectType != null && objectType != null && staticVarName != null)
                                    {
                                        string objectUsed = trimLine.Substring(spaceIndex);
                                        int registerIndex = objectUsed.IndexOf("}") + 3;
                                        if (registerIndex < 0)
                                        {
                                            throw new ArgumentOutOfRangeException("No registers found.\nInvalid invoke method: " + trimLine);
                                        }
                                        string tempObjectUsed = objectUsed.Substring(registerIndex);
                                        int endOfClassIndex = tempObjectUsed.IndexOf(";") + 1;
                                        if (endOfClassIndex < 0)
                                        {
                                            throw new ArgumentOutOfRangeException("No end of class found.\nInvalid invoke method: " + trimLine);
                                        }
                                        string targetObjectClass = tempObjectUsed.Substring(0, endOfClassIndex);
                                        if (targetObjectClass == objectType)
                                        {
                                            // Get all the registers used in the method, but only once for performance sake.
                                            if (usedRegisters == null)
                                            {
                                                usedRegisters = GetUsedRegisters(element);
                                            }
                                            availableRegister = possibleRegisters.Find(x => !usedRegisters.Contains(x));
                                            if (availableRegister == null)
                                            {
                                                throw new ApplicationException("No available register found!");
                                            }

                                            // Update the .locals so we can use our available, local register
                                            string? localsLine = element.GetLines().Find(x => x.Contains(".locals"));
                                            if (localsLine != null)
                                            {
                                                int localsCount = Convert.ToInt32(localsLine.Trim().Substring(8));
                                                localsCount += 1;
                                                string newLocalsLine = "    .locals " + localsCount;
                                                element.ReplaceLine(localsLine, newLocalsLine);
                                            }

                                            // If the line calls the <init> function we need some additional lines to setup our static variable
                                            // Else we can just handle it as any function call
                                            string[]? newLines = null;
                                            if (objectUsed.Contains("-><init>"))
                                            {
                                                overwriteLocalRegister = true;
                                                int firstBracketIndex = objectUsed.IndexOf("{");
                                                int secondBracketIndex = objectUsed.IndexOf("}");
                                                string registerArray = objectUsed.Substring(firstBracketIndex + 1, secondBracketIndex - 2);
                                                string[] registers = registerArray.Split(",");
                                                localRegisterToOverwrite = RemoveSpecialCharacters(registers[0].Trim());

                                                if (opCode != "invoke-static")
                                                {
                                                    newLines = CreateInitLines(availableRegister, currentObjectType, objectUsed, objectType, staticVarName);
                                                }
                                            }
                                            else if (opCode != "invoke-static")
                                            {
                                                newLines = CreateMethodLines(availableRegister, currentObjectType, objectUsed, objectType, opCode, staticVarName);
                                            }

                                            if (newLines != null)
                                            {
                                                element.ReplaceLines(line, newLines);
                                            }
                                        }
                                    }
                                }
                                else if (overwriteLocalRegister && localRegisterToOverwrite != null && availableRegister != null)
                                {
                                    string tempLine = line.Replace(localRegisterToOverwrite, availableRegister);
                                    element.ReplaceLine(line, tempLine);
                                }
                            }
                        }
                    }

                    writeToElements.Add(element);
                }
            }

            string writeToFile = "";
            foreach (Element element in writeToElements)
            {
                writeToFile += element.ToString() + "\n";
            }
            File.WriteAllText(writeToFilePath, writeToFile);
        }
    }
}