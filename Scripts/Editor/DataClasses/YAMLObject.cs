using System;
using System.Collections.Generic;
using System.Text;

namespace UnityEventTracker.DataClasses
{
    internal readonly struct YAMLObject
    {
        public IReadOnlyList<string> Content { get; }
        public int StartingLineIndex { get; }
        public string FileId { get; }

        private const int GuidOffset = 6;
        private const int ScriptOffset = 9;

        public YAMLObject(IReadOnlyList<string> content, int startingLineIndex)
        {
            Content = content;
            StartingLineIndex = startingLineIndex;

            var idLine = Content[0];
            var fileId = idLine.Substring(idLine.IndexOf("&", StringComparison.InvariantCulture) + 1);

            // Like this --- !u!1 &8210284779721349847 stripped
            var indexOfSpace = fileId.IndexOf(' ');
            if (indexOfSpace != -1)
                fileId = fileId.Substring(0, indexOfSpace);

            FileId = fileId;
        }

        public bool IsMonoBehaviour(out string scriptGuid, out string gameObjectId)
        {
            var isMB = Content[1].StartsWith("Mono");

            static void AssignValues(out string s, out string g)
            {
                s = null;
                g = null;
            }

            if (!isMB)
            {
                AssignValues(out scriptGuid, out gameObjectId);
                return false;
            }

            isMB = Content.Count > 8; 
            
            if (!isMB)
            {
                AssignValues(out scriptGuid, out gameObjectId);
                return false;
            }

            // I added this check because some MonoBehaviour
            // have a different structure (for example in Default Style Sheet.asset at Assets/TextMesh Pro/Resources/Style Sheets)
            // so I just skip them
            isMB = Content[ScriptOffset].StartsWith("  m_Script");

            if (!isMB)
            {
                AssignValues(out scriptGuid, out gameObjectId);
                return false;
            }

            var scriptGuidLine = Content[ScriptOffset];
            var indexOfGuid = scriptGuidLine.IndexOf("guid: ", StringComparison.InvariantCulture);

            if (indexOfGuid < 0)
            {
                AssignValues(out scriptGuid, out gameObjectId);
                return false;
            }

            var start = scriptGuidLine.Substring(indexOfGuid + GuidOffset);
            scriptGuid = start.Substring(0, start.IndexOf(','));

            const int offset = 6;
            var line = Content[offset]; //m_GameObject: {fileID: 566893015}
            var gameObjectIdStart = line.LastIndexOf(' ') + 1;
            gameObjectId = line.Substring(gameObjectIdStart).TrimEnd('}');

            return true;
        }

        public bool IsGameObject(out string id)
        {
            var isGO = Content[1].StartsWith("Game");

            static void AssignValues(out string i)
            {
                i = null;
            }

            if (!isGO)
            {
                AssignValues(out id);
                return false;
            }

            id = FileId;
            return true;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            for (var i = 0; i < Content.Count; i++)
            {
                sb.AppendLine(Content[i]);
            }

            return sb.ToString();
        }
    }
}