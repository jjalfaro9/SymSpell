using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace symspell.Riley {
    public class WordReader {

        private static HashSet<string> get_words_reg_ex(string path) {
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

        public static (HashSet<string>, Dictionary<string, int>) get_words(string path) {
            HashSet<string> words = new HashSet<string>();
            int wc = 0;
            Dictionary<string, int> wordToIndex = new Dictionary<string, int>();
            
            foreach (string line in File.ReadAllLines(path)) {
                var x = line.Split('\t');
                wc += 1;
                if (words.Add(x[0])) {
                    wordToIndex.Add(x[0], Int32.Parse(x[1]));
                }
                // what to do about words that are repeated ? 
            }
            Console.WriteLine("path : " + path);
            Console.WriteLine("wc : " + wc);
            Console.WriteLine("set size : " + words.Count);
            Console.WriteLine("dictionary size :  " + wordToIndex.Count);
            return (words, wordToIndex);
        }
    }
}