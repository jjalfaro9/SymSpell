using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;


namespace symspell.Riley {
    public class Riley {

        interface Vocab {
            bool isSource();

            string getValue();
        }

        class Target : Vocab {
            public string s;

            public Target(string t) {
                this.s = t;
            } 
            public bool isSource() { return false; }

            public string getValue() { return s; }
        }

        class Source : Vocab {
            public string s;

            public Source(string t) {
                this.s = t;
            }

            public bool isSource() { return true; }

            public string getValue() { return s; }
        }

        const int defaultMaxEditDistance = 2;
        const int defaultPrefixLength = 7;
        const int defaultCompactLevel = 5;

        private Dictionary<int, HashSet<Vocab>> deletesToKeyWords;

        EditDistance distanceComparer = new EditDistance(EditDistance.DistanceAlgorithm.Levenshtein);
        private int maxDictionaryEditDistance;
        private int prefixLength;
        private byte compactLevel;
        private uint compactMask;

        public Riley(int maxDictionaryEditDistance = defaultMaxEditDistance, int prefixLength = defaultPrefixLength, byte compactLevel = defaultCompactLevel) {
            this.maxDictionaryEditDistance = maxDictionaryEditDistance;
            this.prefixLength = prefixLength;
            this.compactLevel = compactLevel;
            this.compactMask = (uint.MaxValue >> (3 + this.compactLevel)) << 2;
            this.deletesToKeyWords = new Dictionary<int, HashSet<Vocab>>();
        }

        //check whether all delete chars are present in the suggestion prefix in correct order, otherwise this is just a hash collision
        private bool DeleteInSuggestionPrefix(string delete, int deleteLen, string suggestion, int suggestionLen)
        {
            if (deleteLen == 0) return true;
            if (prefixLength < suggestionLen) suggestionLen = prefixLength;
            int j = 0;
            for (int i = 0; i < deleteLen; i++)
            {
                char delChar = delete[i];
                while (j < suggestionLen && delChar != suggestion[j]) j++;
                if (j == suggestionLen) return false;
            }
            return true;
        }

        //inexpensive and language independent: only deletes, no transposes + replaces + inserts
        //replaces and inserts are expensive and language dependent (Chinese has 70,000 Unicode Han characters)
        private HashSet<string> Edits(string word, int editDistance, HashSet<string> deleteWords)
        {
            editDistance++;
            if (word.Length > 1)
            {
                for (int i = 0; i < word.Length; i++)
                {
                    string delete = word.Remove(i, 1);
                    if (deleteWords.Add(delete))
                    {
                        //recursion, if maximum edit distance not yet reached
                        if (editDistance < maxDictionaryEditDistance) Edits(delete, editDistance, deleteWords);
                    }
                }
            }
            return deleteWords;
        }

        private HashSet<string> EditsPrefix(string key)
        {
            HashSet<string> hashSet = new HashSet<string>();
            if (key.Length <= maxDictionaryEditDistance) hashSet.Add("");
            if (key.Length > prefixLength) key = key.Substring(0, prefixLength);
            hashSet.Add(key);
            return Edits(key, 0, hashSet);
        }

        private int GetStringHash(string s)
        {
            //return s.GetHashCode();

            int len = s.Length;
            int lenMask = len;
            if (lenMask > 3) lenMask = 3;

            uint hash = 2166136261;
            for (var i = 0; i < len; i++)
            {
                unchecked
                {
                    hash ^= s[i];
                    hash *= 16777619;
                }
            }

            hash &= this.compactMask;
            hash |= (uint)lenMask;
            return (int)hash;
        }

        private void add_values_to_dictionary(HashSet<string> keys, Vocab value) {
            foreach (string key in keys) {
                int keyHash = GetStringHash(key);
                if (deletesToKeyWords.TryGetValue(keyHash, out HashSet<Vocab> words)) {
                    // var newWordCollision = new Vocab[words.Length + 1];
                    // Array.Copy(words, newWordCollision, words.Length);
                    // deletesToKeyWords[keyHash] = words = newWordCollision;
                } else {
                    words = new HashSet<Vocab>();
                    deletesToKeyWords.Add(keyHash, words);
                }
                words.Add(value);
                // words[words.Length - 1 ] = value;
            }
        }

        private HashSet<(string, string)> get_pairs(HashSet<string> src, HashSet<string> trg, int k) {
            HashSet<(string, string)> similar = new HashSet<(string, string)>();
            // not liking the O(S*T), but I think we have to compare all of 'em.
            foreach (string s in src) {
                var sLen = s.Length;
                var sPrefixLen = (sLen > prefixLength) ? prefixLength : sLen;
                foreach (string t in trg) {
                    
                    var tLen = t.Length;
                    if ((Math.Abs(sLen - tLen) > k)
                        || (tLen < sLen)
                        || (tLen == sLen && t != s)
                    ) {
                        continue;
                    }
                    
                    var tPrefixLen = Math.Min(tLen, prefixLength);
                    if (tPrefixLen > sPrefixLen && (tPrefixLen - sLen) > k) { continue; }

                    int distance = 0;
                    int min = 0;
                    if (sLen == 0) {
                        distance = Math.Max(sLen, tLen);
                        if (distance > k ) { continue; } // need to consider the hashSet2
                    } else if (tLen == 1) {
                        if (s.IndexOf(t[0]) < 0) { distance = tLen; } else { distance = tLen - 1; }
                        if (distance > k) { continue; } // need to consider the hashSet2
        
                    } else {
                        if ( (prefixLength - k == tLen)
                                && (((min = Math.Min(sLen, tLen) - prefixLength) > 1)
                                    && (s.Substring(sLen + 1 - min) != t.Substring(tLen + 1 - min)))
                                || ((min > 0) && (s[sLen - min] != t[tLen - min])
                                    && ((s[sLen - min - 1] != t[tLen - min])
                                            || (s[sLen - min] != t[tLen - min - 1]))))
                        { continue; }
                        else {
                            if (!DeleteInSuggestionPrefix(t, tLen, s, sLen)) { continue; } // check for hashSet2 thingy
                            distance = distanceComparer.Compare(s, t, k);
                            if (distance < 0) { continue; }
                        }

                        if (distance <= k) {
                            // we good to add these two as a pair! 
                            // should probably see if the list is going to be unique! 
                            similar.Add((s, t));
                        }
                                
                    }
                }
            }
            return similar;
        }
        public HashSet<(string, string)> generate_src_trg_pairs(HashSet<string> target, HashSet<string> source, int maxEditDistance) {
            Console.WriteLine("intersection size : " + target.Intersect(source).Count());
            foreach (string trg in target) {
                add_values_to_dictionary(EditsPrefix(trg), new Target(trg));
            }

            foreach (string src in source) {
                add_values_to_dictionary(EditsPrefix(src), new Source(src));
            }

            HashSet<(string, string)> pairsSharingBins = new HashSet<(string, string)>();
            foreach (HashSet<Vocab> collisions in this.deletesToKeyWords.Values.Where(s => s.Count > 1)) {
            // foreach (Vocab[] collisions in this.deletesToKeyWords.Values.Where(l => l.Length > 1)) {
                var groups = collisions.GroupBy(x => x.isSource(), x => x.getValue())
                                        .ToDictionary(g => g.Key, g => g.ToHashSet());
               // but what is going on with the other stuff?
                HashSet<string> s;
                HashSet<string> t;
                if (groups.TryGetValue(true, out s) && groups.TryGetValue(false, out t)) {
                    pairsSharingBins.UnionWith(get_pairs(s, t, maxEditDistance));
                }
            }

            return pairsSharingBins;
        }

        public List<(string, string, double)> generate_NL(HashSet<(string,string)> src_trg_pairs) {
            List<(string, string, double)> pairs_with_NL = new List<(string, string, double)>();
            foreach ((string, string) pair in src_trg_pairs) {
                var l = distanceComparer.distance(pair.Item1, pair.Item2);
                var m = Math.Max(pair.Item1.Length, pair.Item2.Length);
                pairs_with_NL.Add((pair.Item1, pair.Item2, l / m));
            }
            return pairs_with_NL;
        }

        public List<(string, string, double, double)> generate_orthographic_similarity_score(List<(string, string, double)> src_trg_NL_pairs) {
            List<(string, string, double, double)> pairs_with_sim = new List<(string, string, double, double)>();
            foreach ((string, string, double) trip in src_trg_NL_pairs) {
                var sim = Math.Log10(2.0 - trip.Item3);
                pairs_with_sim.Add((trip.Item1, trip.Item2, trip.Item3, sim));
            }
            return pairs_with_sim;
        }
        static int Main(string[] args) {
            if (args.Length != 4) {
                return 1;
            }

            var r = new Riley();

            var words = WordReader.get_src_trg_words(args[0], args[1]);
            var src_trg_pairs = r.generate_src_trg_pairs(words.Item2, words.Item1, Int32.Parse(args[2]));
            Console.WriteLine("src_trg_pairs size : " + src_trg_pairs.Count);

            var src_trg_NL_trips = r.generate_NL(src_trg_pairs);
            List<(string, string, double, double)> src_trg_NL_sim_quarts = r.generate_orthographic_similarity_score(src_trg_NL_trips);
            using (StreamWriter sw = new StreamWriter(args[3])) {
                sw.WriteLine("src,trg,NL,Sim_Score");
                foreach ((string, string, double, double) data in src_trg_NL_sim_quarts) {
                    sw.WriteLine(data.Item1 + "," + data.Item2 + "," + data.Item3 + "," + data.Item4);
                }
                sw.Flush();
                sw.Close();
            }
        

            // File.WriteAllLines(args[3], src_trg_NL_sim_quarts); // iffy lol

            Console.WriteLine("Hola mi mundo, k lo k!");
            
            
            return 0;
        }
    }
}