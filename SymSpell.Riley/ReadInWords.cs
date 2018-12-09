using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace symspell.Riley {
    public class WordReader {

        private static HashSet<string> get_words(string path) {
            HashSet<string> words = new HashSet<string>();
            int wc = 0;
            string pattern = @"[^\W\d](\w|[-'.//]{1,2}(?=\w))*";
           
            foreach (string line in File.ReadAllLines(path)) {
                var r = Regex.Match(line, pattern);
                if (r.Success) {
                    words.Add(r.ToString());
                    wc += 1;
                }
            }
            
            Console.WriteLine("file : " + path);
            Console.WriteLine("wc : " + wc);
            Console.WriteLine("set len : " + words.Count);
            
            return words;
        }
        public static (HashSet<string>, HashSet<string>) get_src_trg_words(string src_path, string trg_path) {
            return (get_words(src_path), get_words(trg_path));
        }
    }
}