using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmaliConsolidator
{
    internal class Element
    {
        public string Type { get; private set; }
        public string Identifier { get; private set; }

        private bool hasEnd = false;
        private List<string> lines;

        public Element(string firstLine)
        {
            Type = firstLine.Substring(1, firstLine.IndexOf(" ") - 1);
            Identifier = firstLine.Substring(firstLine.LastIndexOf(" "));
            lines = new List<string>();
            lines.Add(firstLine);
        }

        public void AddLine(string line)
        {
            if(hasEnd)
            {
                lines.Insert(lines.Count - 2, line);
            }
            else
            {
                if (line == ".end " + Type)
                {
                    hasEnd = true;
                }

                lines.Add(line);
            }
        }

        public void ReplaceLine(string oldLine, string newLine)
        {
            int insertIndex = lines.IndexOf(oldLine);
            lines.Remove(oldLine);
            lines.Insert(insertIndex, newLine);
        }

        public void ReplaceLines(string oldLine, string[] newLines)
        {
            int insertIndex = lines.IndexOf(oldLine);
            lines.Remove(oldLine);
            foreach(string line in newLines)
            {
                lines.Insert(insertIndex, line);
                insertIndex += 1;
            }
        }

        public void RemoveLine(string line)
        {
            lines.Remove(line);
        }

        public List<string> GetLines()
        {
            return new List<string>(lines);
        }

        public override string ToString()
        {
            string returnString = "";
            foreach(string line in lines)
            {
                returnString += line + "\n\n";
            }
            return returnString;
        }
    }
}
