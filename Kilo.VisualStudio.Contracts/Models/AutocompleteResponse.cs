using System.Collections.Generic;

namespace Kilo.VisualStudio.Contracts.Models
{
    public class AutocompleteResponse
    {
        public List<CompletionItem> Completions { get; set; } = new List<CompletionItem>();
    }

    public class CompletionItem
    {
        public string Label { get; set; } = string.Empty;
        public CompletionKind Kind { get; set; }
        public string Detail { get; set; } = string.Empty;
        public string Documentation { get; set; } = string.Empty;
        public string InsertText { get; set; } = string.Empty;
    }

    public enum CompletionKind
    {
        Text,
        Method,
        Function,
        Constructor,
        Field,
        Variable,
        Class,
        Interface,
        Module,
        Property,
        Unit,
        Value,
        Enum,
        Keyword,
        Snippet,
        Color,
        File,
        Reference,
        Folder,
        EnumMember,
        Constant,
        Struct,
        Event,
        Operator,
        TypeParameter
    }
}