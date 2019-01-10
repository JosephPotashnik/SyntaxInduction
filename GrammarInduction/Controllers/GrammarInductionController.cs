using System;
using System.Collections.Generic;
using System.Linq;
using LinearIndexedGrammarParser;
using Microsoft.AspNetCore.Mvc;

namespace GrammarInduction.Controllers
{
    [Route("api/SyntaxInduction")]
    public class GrammarInductionController : Controller
    {

        private string ReplaceContractions(string sentence)
        {
            string s = sentence.Replace("'ll", " will"); //ambiguous: I'll = I will / I shall
            string s1 = s.Replace("'ve", " have");
            string s2 = s1.Replace("'m", " am");
            string s3 = s2.Replace("'d", " had"); //ambiguous: I'd = I had / I would, how'd = how did / how would.
            string s4 = s3.Replace("n't", " not");
            string s5 = s4.Replace("'re", " are");

            //
            //string s6 = s5.Replace("'s", " is"); //ambiguous: it's = it is / it has
            //another problem: 's could be also the possessive:
            //[john's father] -> not! [john is/has father].
            return s5;

        }
        [HttpPost]
        [Route("Learn/{textFileName}")]
        public void Learn(string textFileName)
        {

            Vocabulary uniVocabulary = Vocabulary.ReadVocabularyFromFile("UniversalVocabulary.json");
            //read sentences. (unused delimiters for sentence level: ' ' and '-')
            var sentenceDelimiters = new[]
            {'(', ')', '?', ',', '*', '.', ';', '!', '\\', '/', ':', '"', '“', '—', '”'};
            var filestring = System.IO.File.ReadAllText(textFileName);
            var sentences = filestring.Split(sentenceDelimiters, StringSplitOptions.RemoveEmptyEntries);
            //discard empty spaces, lowercase. I will not be concerned with capitalization for now.
            var sentences1 = sentences.Select(sentence => sentence.TrimStart()).Select(sentence => sentence.ToLower());
            sentences1 = sentences1.Select(sentence => sentence.Replace('\r', ' '));
            sentences1 = sentences1.Select(sentence => sentence.Replace('\n', ' '));

            var encounteredWords = new HashSet<string>();
            HashSet<string> wordsNotInVocabulary = new HashSet<string>();

            List<string[]> sentencesToLearn = new List<string[]>();
            foreach (var sentence1 in sentences1)
            {
                //first stage of preprocessing =
                //replace contractions with full words.
                var sentence = ReplaceContractions(sentence1);
                bool unableToResolveWord = false;

                //split to words
                var sentenceWords = sentence.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (sentenceWords.Count() < 3) continue; // don't include sentences less than 3 words.
                //temporary - you are exploring whether you can induce any grammar at all from real text.
                if (sentenceWords.Count() > 5) continue; // don't treat sentences more than 5 words. 

                Dictionary<string, List<string>> newWords = new Dictionary<string, List<string>>();

                newWords["N"] = new List<string>();
                newWords["V"] = new List<string>();
                newWords["ADJ"] = new List<string>();
                newWords["ADV"] = new List<string>();

                //second stage of preprocessing
                //go over each word in the sentence
                //if it does not appear in dictionary,
                //infer its base form and part of speech from known conjugations
                //(i.e ing/ed/s , er/est, etc)
                string prevWord = null;
                List<string> s = new List<string>();
                foreach (var wordOrig in sentenceWords)
                {
                    //trim leading or trailing single apostrophe
                    //(sometimes text come with single quotation marks: 'did you see him?'
                    //this case needs to be differentiated from single apostrophe used
                    //for contractions: I'd, can't, isn't...
                    var word1 = wordOrig.TrimStart('\'');
                    var word = word1.TrimEnd('\'');

                    if (wordsNotInVocabulary.Contains(word))
                    {
                        unableToResolveWord = true;
                        break;
                    }

                    if (encounteredWords.Contains(word))
                    {
                        if (prevWord != null && prevWord == "to" && uniVocabulary.WordWithPossiblePOS[word].Contains("V"))
                        {
                            unableToResolveWord = true;
                            break;
                        }

                        prevWord = word;
                        s.Add(word);
                        continue;
                    }
                    encounteredWords.Add(word);
                    unableToResolveWord = true;

                    if (word.EndsWith("ed"))
                    {
                        newWords["V"].Add(word);
                        newWords["ADJ"].Add(word);
                        unableToResolveWord = false;

                    }
                    else if (word.EndsWith("ing"))
                    {
                        newWords["V"].Add(word);
                        newWords["ADJ"].Add(word);
                        unableToResolveWord = false;

                    }
                    else if (word.EndsWith("s") || word.EndsWith("es") || word.EndsWith("ies"))
                    {
                        List<string> baseWords = new List<string>();
                        //"s"
                        baseWords.Add(word.Substring(0, word.Length - 1));

                        if (word.EndsWith("ies"))
                            baseWords.Add(word.Substring(0, word.Length - 3) + "y");
                        else if (word.EndsWith("es"))
                            baseWords.Add(word.Substring(0, word.Length - 2));

                        foreach (var baseWord in baseWords)
                        {
                            if (uniVocabulary.ContainsWord(baseWord))
                            {
                                var possiblePOS = uniVocabulary.WordWithPossiblePOS[baseWord];

                                if (possiblePOS.Contains("N"))
                                {
                                    newWords["N"].Add(word);
                                    unableToResolveWord = false;
                                }

                                if (possiblePOS.Contains("V"))
                                {
                                    newWords["V"].Add(word);
                                    unableToResolveWord = false;

                                }

                                //adverbs and adjectives in English do not conjugate.
                            }
                        }
                        
                    }
                    //superlative (more)
                    else if (word.EndsWith("er") || word.EndsWith("ier"))
                    {
                        List<string> baseWords = new List<string>();
                        //"er"
                        baseWords.Add(word.Substring(0, word.Length - 1)); //safer -> safe
                        baseWords.Add(word.Substring(0, word.Length - 2)); //thicker -> thick

                        if (word.EndsWith("ier"))
                            baseWords.Add(word.Substring(0, word.Length - 3) + "y");

                        foreach (var baseWord in baseWords)
                        {
                            if (uniVocabulary.ContainsWord(baseWord))
                            {
                                var possiblePOS = uniVocabulary.WordWithPossiblePOS[baseWord];

                                if (possiblePOS.Contains("ADJ"))
                                {
                                    unableToResolveWord = false;
                                    newWords["ADJ"].Add(word);
                                }
                            }
                        }
                    } 
                    //superlatives (most)
                    else if (word.EndsWith("est") || word.EndsWith("iest"))
                    {
                        List<string> baseWords = new List<string>();
                        //"est"
                        baseWords.Add(word.Substring(0, word.Length - 3)); //thickest -> thick
                        baseWords.Add(word.Substring(0, word.Length - 2)); //safest -> safe

                        if (word.EndsWith("iest"))
                            baseWords.Add(word.Substring(0, word.Length - 4) + "y");

                        foreach (var baseWord in baseWords)
                        {
                            if (uniVocabulary.ContainsWord(baseWord))
                            {
                                var possiblePOS = uniVocabulary.WordWithPossiblePOS[baseWord];

                                if (possiblePOS.Contains("ADJ"))
                                {
                                    newWords["ADJ"].Add(word);
                                    unableToResolveWord = false;
                                }
                            }
                        }
                    }

                    if (!uniVocabulary.ContainsWord(word))
                    {
                        if (unableToResolveWord == true)
                        {
                            wordsNotInVocabulary.Add(word);
                            break;
                        }

                    }
                    else if (uniVocabulary.WordWithPossiblePOS[word].Contains("V"))
                    {
                        if (word == "cry")
                        {
                            int x = 1;
                        }
                        if (prevWord != null && prevWord == "to")
                        {
                            unableToResolveWord = true;
                            break;
                        }
                    }

                    unableToResolveWord = false;
                    prevWord = word;
                    s.Add(word);
                }

                foreach (var pos in newWords.Keys)
                    uniVocabulary.AddWordsToPOSCategory(pos, newWords[pos].ToArray());

                if (unableToResolveWord == false)
                    sentencesToLearn.Add(s.ToArray());
                
            }

            using (System.IO.StreamWriter file =
                new System.IO.StreamWriter(@"CorwinToLearn.txt"))
            {
                foreach (var sen in sentencesToLearn)
 
                    file.WriteLine(string.Join(" ", sen));
            }
        }
    }
}