using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Runtime;
using PKHeX.Core.Enhancements;
namespace PKHeX.Core.AutoMod
{
    /// <summary>
    /// Miscellaneous enhancement methods
    /// </summary>
    public static class ModLogic
    {
        // Living Dex Settings
        public static LivingDexConfig cfg = new()
        {
            IncludeForms = false,
            SetShiny = false,
            SetAlpha = false,
            NativeOnly = false,
            TransferVersion = GameVersion.SL
        };
        public static bool IncludeForms { get; set; }
        public static bool SetShiny { get; set; }
        public static bool SetAlpha { get; set; }
        public static bool NativeOnly { get; set; }
        public static GameVersion TransferVersion { get; set; }

        /// <summary>
        /// Exports the <see cref="SaveFile.CurrentBox"/> to <see cref="ShowdownSet"/> as a single string.
        /// </summary>
        /// <param name="provider">Save File to export from</param>
        /// <returns>Concatenated string of all sets in the current box.</returns>
        public static string GetRegenSetsFromBoxCurrent(this ISaveFileProvider provider) => GetRegenSetsFromBox(provider.SAV, provider.CurrentBox);

        /// <summary>
        /// Exports the <see cref="box"/> to <see cref="ShowdownSet"/> as a single string.
        /// </summary>
        /// <param name="sav">Save File to export from</param>
        /// <param name="box">Box to export from</param>
        /// <returns>Concatenated string of all sets in the specified box.</returns>
        public static string GetRegenSetsFromBox(this SaveFile sav, int box)
        {
            var data = sav.GetBoxData(box);
            var sep = Environment.NewLine + Environment.NewLine;
            return data.GetRegenSets(sep);
        }

        /// <summary>
        /// Gets a living dex (one per species, not every form)
        /// </summary>
        /// <param name="sav">Save File to receive the generated <see cref="PKM"/>.</param>
        /// <returns>Consumable list of newly generated <see cref="PKM"/> data.</returns>
        public static IEnumerable<PKM> GenerateLivingDex(this SaveFile sav) => sav.GenerateLivingDex(cfg);

        /// <summary>
        /// Gets a living dex (one per species, not every form)
        /// </summary>
        /// <param name="sav">Save File to receive the generated <see cref="PKM"/>.</param>
        /// <returns>Consumable list of newly generated <see cref="PKM"/> data.</returns>
        public static IEnumerable<PKM> GenerateLivingDex(this SaveFile sav, LivingDexConfig cfg)
        {
            var pklist = new ConcurrentBag<PKM>();
            List<List<PKM>> Initialpklist = [];
            var tr = APILegality.UseTrainerData ? TrainerSettings.GetSavedTrainerData(sav.Version, sav.Generation, fallback: sav, lang: (LanguageID)sav.Language) : sav;
            var pt = sav.Personal;
            var species = Enumerable.Range(1, sav.MaxSpeciesID).Select(x => (ushort)x);
            Parallel.ForEach(species, s =>
            {
                 if (!pt.IsSpeciesInGame(s))
                      return;

                 var num_forms = pt[s].FormCount;
                 var str = GameInfo.Strings;
                 if (num_forms == 1 && cfg.IncludeForms) // Validate through form lists
                      num_forms = (byte)FormConverter.GetFormList(s, str.types, str.forms, GameInfo.GenderSymbolUnicode, sav.Context).Length;

                 for (byte f = 0; f < num_forms; f++)
                 {
                     if (!sav.Personal.IsPresentInGame(s, f) || FormInfo.IsLordForm(s, f, sav.Context) || FormInfo.IsBattleOnlyForm(s, f, sav.Generation) || FormInfo.IsFusedForm(s, f, sav.Generation) || (FormInfo.IsTotemForm(s, f) && sav.Context is not EntityContext.Gen7))
                         continue;

                     var form = cfg.IncludeForms ? f : GetBaseForm(s, f, sav);
                     var pk = AddPKM(sav, tr, s, f, cfg.SetShiny, cfg.SetAlpha, cfg.NativeOnly);
                     if (pk is not null && !pklist.Any(x => x.Species == pk.Species && x.Form == pk.Form))
                     {
                          pklist.Add(pk);
                          if (!cfg.IncludeForms)
                              break;
                     }
                 }
            });
            return pklist.OrderBy(z=>z.Species);
        }
        public static IEnumerable<PKM> GenerateTLivingDex(this SaveFile sav) => sav.GenerateTLivingDex(cfg);
        public static IEnumerable<PKM> GenerateTLivingDex(this SaveFile sav, LivingDexConfig cfg)
        {
            var resetevent = new ManualResetEvent(false);
            var DestinationSave = SaveUtil.GetBlankSAV(cfg.TransferVersion, "ALM");
            ConcurrentBag<PKM> pklist = [];
            List<List<PKM>> Initialpklist = [];
            var tr = APILegality.UseTrainerData ? TrainerSettings.GetSavedTrainerData(sav.Version, sav.Generation, fallback: sav, lang: (LanguageID)sav.Language) : sav;
            var pt = sav.Personal;
            var species = Enumerable.Range(1, sav.MaxSpeciesID).Select(x => (ushort)x);
            Parallel.ForEach(species, s =>
            {
                if (!pt.IsSpeciesInGame(s))
                    return;
                if (!DestinationSave.Personal.IsSpeciesInGame(s))
                    return;
                var num_forms = pt[s].FormCount;
                var str = GameInfo.Strings;
                if (num_forms == 1 && cfg.IncludeForms) // Validate through form lists
                    num_forms = (byte)FormConverter.GetFormList(s, str.types, str.forms, GameInfo.GenderSymbolUnicode, sav.Context).Length;

                for (byte f = 0; f < num_forms; f++)
                {
                    if (!DestinationSave.Personal.IsPresentInGame(s, f) || FormInfo.IsLordForm(s, f, sav.Context) || FormInfo.IsBattleOnlyForm(s, f, sav.Generation) || FormInfo.IsFusedForm(s, f, sav.Generation) || (FormInfo.IsTotemForm(s, f) && sav.Context is not EntityContext.Gen7))
                        continue;

                    var form = cfg.IncludeForms ? f : GetBaseForm(s, f, sav);
                    var pk = AddPKM(sav, tr, s, f, cfg.SetShiny, cfg.SetAlpha, cfg.NativeOnly);
                    if (pk is not null && !pklist.Any(x => x.Species == pk.Species && x.Form == pk.Form))
                    {
                        pklist.Add(pk);
                        if (!cfg.IncludeForms)
                            break;
                    }
                }
            });
            return pklist.OrderBy(z => z.Species);
        }
        public static byte GetBaseForm(ushort s, byte f, SaveFile sav)
        {
            List<Species> SV = [Species.Tauros, Species.Wooper];
            List<Species> LA = [Species.Growlithe, Species.Arcanine, Species.Voltorb, Species.Electrode, Species.Typhlosion, Species.Qwilfish, Species.Sneasel, Species.Samurott, Species.Lilligant, Species.Zorua, Species.Zoroark, Species.Braviary, Species.Sliggoo, Species.Goodra, Species.Avalugg, Species.Decidueye];
            List<Species> SH = [Species.Meowth, Species.Slowpoke, Species.Ponyta, Species.Rapidash, Species.Slowbro, Species.Slowking, Species.Farfetchd, Species.Weezing, Species.MrMime, Species.Articuno, Species.Moltres, Species.Zapdos, Species.Corsola, Species.Zigzagoon, Species.Linoone, Species.Darumaka, Species.Darmanitan, Species.Yamask, Species.Stunfisk];
            List<Species> SM = [Species.Rattata, Species.Raticate, Species.Raichu, Species.Sandshrew, Species.Sandslash, Species.Vulpix, Species.Ninetales, Species.Diglett, Species.Dugtrio, Species.Meowth, Species.Persian, Species.Geodude, Species.Graveler, Species.Golem, Species.Grimer, Species.Muk, Species.Marowak];
            var HasRegionalForm = sav.Version switch
            {
                GameVersion.VL or GameVersion.SL => SV.Contains((Species)s),
                GameVersion.PLA => LA.Contains((Species)s),
                GameVersion.SH or GameVersion.SW => SH.Contains((Species)s),
                GameVersion.SN or GameVersion.MN or GameVersion.UM or GameVersion.US => SM.Contains((Species)s),
                _ => false,
            };
            if (HasRegionalForm)
            {
                if (sav.Version is GameVersion.SW or GameVersion.SH && ((Species)s == Species.Slowbro || (Species)s == Species.Meowth || (Species)s == Species.Darmanitan))
                    return 2;
                return 1;
            }
            else
            {
                return f;
            }
        }
        private static PKM? AddPKM(SaveFile sav, ITrainerInfo tr, ushort species, byte form, bool shiny, bool alpha, bool nativeOnly)
        {
            if (tr.GetRandomEncounter(species, form, shiny, alpha, nativeOnly, out var pk) && pk?.Species > 0)
            {
                pk.Heal();
                return pk;
            }
            return sav is SAV2 && GetRandomEncounter(new SAV1(GameVersion.Y) { Language = tr.Language, OT = tr.OT, TID16 = tr.TID16 }, species, form, shiny, false, nativeOnly, out var pkm) && pkm is PK1 pk1 ? pk1.ConvertToPK2() : (PKM?)null;
        }

        /// <summary>
        /// Gets a legal <see cref="PKM"/> from a random in-game encounter's data.
        /// </summary>
        /// <param name="sav">Save File to receive the generated <see cref="PKM"/>.</param>
        /// <param name="species">Species ID to generate</param>
        /// <param name="form">Form to generate; if left null, picks first encounter</param>
        /// <param name="shiny"></param>
        /// <param name="alpha"></param>
        /// <param name="pk">Result legal pkm</param>
        /// <returns>True if a valid result was generated, false if the result should be ignored.</returns>
        public static bool GetRandomEncounter(this SaveFile sav, ushort species, byte form, bool shiny, bool alpha, bool nativeOnly, out PKM? pk) => ((ITrainerInfo)sav).GetRandomEncounter(species, form, shiny, alpha, nativeOnly, out pk);

        /// <summary>
        /// Gets a legal <see cref="PKM"/> from a random in-game encounter's data.
        /// </summary>
        /// <param name="tr">Trainer Data to use in generating the encounter</param>
        /// <param name="species">Species ID to generate</param>
        /// <param name="form">Form to generate; if left null, picks first encounter</param>
        /// <param name="shiny"></param>
        /// <param name="alpha"></param>
        /// <param name="pk">Result legal pkm</param>
        /// <returns>True if a valid result was generated, false if the result should be ignored.</returns>
        public static bool GetRandomEncounter(this ITrainerInfo tr, ushort species, byte form, bool shiny, bool alpha, bool nativeOnly, out PKM? pk)
        {
            var blank = EntityBlank.GetBlank(tr);
            pk = GetRandomEncounter(blank, tr, species, form, shiny, alpha, nativeOnly);
            if (pk is null)
                return false;

            pk = EntityConverter.ConvertToType(pk, blank.GetType(), out _);
            return pk is not null;
        }

        /// <summary>
        /// Gets a legal <see cref="PKM"/> from a random in-game encounter's data.
        /// </summary>
        /// <param name="blank">Template data that will have its properties modified</param>
        /// <param name="tr">Trainer Data to use in generating the encounter</param>
        /// <param name="species">Species ID to generate</param>
        /// <param name="form">Form to generate; if left null, picks first encounter</param>
        /// <param name="shiny"></param>
        /// <param name="alpha"></param>
        /// <returns>Result legal pkm, null if data should be ignored.</returns>
        private static PKM? GetRandomEncounter(PKM blank, ITrainerInfo tr, ushort species, byte form, bool shiny, bool alpha, bool nativeOnly)
        {
            blank.Species = species;
            blank.Gender = blank.GetSaneGender();
            if (species is ((ushort)Species.Meowstic) or ((ushort)Species.Indeedee))
            {
                blank.Gender = form;
                blank.Form = (byte)blank.Gender;
            }
            else
            {
                blank.Form = form;
            }

            var template = EntityBlank.GetBlank(tr.Generation, tr.Version);
            var item = GetFormSpecificItem((int)tr.Version, blank.Species, blank.Form);
            if (item is not null)
                blank.HeldItem = (int)item;

            if (blank.Species == (ushort)Species.Keldeo && blank.Form == 1)
                blank.Move1 = (ushort)Move.SecretSword;

            if (blank.GetIsFormInvalid(tr, blank.Form))
                return null;

            var setText = new ShowdownSet(blank).Text.Split('\r')[0];
            if (shiny && !SimpleEdits.IsShinyLockedSpeciesForm(blank.Species, blank.Form)&&(tr.Generation!=6 && blank.Species != (ushort)Species.Vivillon && blank.Form !=18))
                setText += Environment.NewLine + "Shiny: Yes";

            if (template is IAlphaReadOnly && alpha && tr.Version == GameVersion.PLA)
                setText += Environment.NewLine + "Alpha: Yes";

            var sset = new ShowdownSet(setText);
            var set = new RegenTemplate(sset) { Nickname = string.Empty };
            template.ApplySetDetails(set);

            var t = template.Clone();
            var almres = tr.TryAPIConvert(set, t, nativeOnly);
            var pk = almres.Created;
            var success = almres.Status;
            if (pk.Species == (ushort)Species.Unown && pk.Form != blank.Form)
                pk.Form = blank.Form;

            if (success == LegalizationResult.Regenerated)
                return pk;

            sset = new ShowdownSet(setText.Split('\r')[0]);
            set = new RegenTemplate(sset) { Nickname = string.Empty };
            template.ApplySetDetails(set);

            t = template.Clone();
            almres = tr.TryAPIConvert(set, t, nativeOnly);
            pk = almres.Created;
            success = almres.Status;
            if (pk.Species is (ushort)Species.Gholdengo)
            {
                pk.SetSuggestedFormArgument();
                pk.SetSuggestedMoves();
                success = LegalizationResult.Regenerated;
            }

            return success == LegalizationResult.Regenerated ? pk : null;
        }

        private static bool GetIsFormInvalid(this PKM pk, ITrainerInfo tr, byte form)
        {
            var generation = tr.Generation;
            var species = pk.Species;
            switch ((Species)species)
            {
                case Species.Unown when generation == 2 && form >= 26:
                    return true;
                case Species.Floette when form == 5:
                    return true;
                case Species.Shaymin
                or Species.Furfrou
                or Species.Hoopa when form != 0 && generation <= 6:
                    return true;
                case Species.Arceus when generation == 4 && form == 9: // ??? form
                    return true;
                case Species.Scatterbug or Species.Spewpa when form == 19:
                    return true;
            }
            if (FormInfo.IsBattleOnlyForm(pk.Species, form, generation))
                return true;

            if (form == 0)
                return false;

            if (species == 25 || SimpleEdits.AlolanOriginForms.Contains(species))
            {
                if (generation >= 7 && pk.Generation is < 7 and not 0)
                    return true;
            }

            return false;
        }

        private static int? GetFormSpecificItem(int game, int species, int form)
        {
            if (game == (int)GameVersion.PLA)
                return null;

            var generation = ((GameVersion)game).GetGeneration();
            return species switch
            {
                (ushort)Species.Arceus => generation != 4 || form < 9 ? SimpleEdits.GetArceusHeldItemFromForm(form) : SimpleEdits.GetArceusHeldItemFromForm(form - 1),
                (ushort)Species.Silvally => SimpleEdits.GetSilvallyHeldItemFromForm(form),
                (ushort)Species.Genesect => SimpleEdits.GetGenesectHeldItemFromForm(form),
                (ushort)Species.Giratina => form == 1 && generation < 9 ? 112 : form == 1 ? 1779 : null, // Griseous Orb
                (ushort)Species.Zacian => form == 1 ? 1103 : null, // Rusted Sword
                (ushort)Species.Zamazenta => form == 1 ? 1104 : null, // Rusted Shield
                _ => null
            };
        }

        /// <summary>
        /// Legalizes all <see cref="PKM"/> in the specified <see cref="box"/>.
        /// </summary>
        /// <param name="sav">Save File to legalize</param>
        /// <param name="box">Box to legalize</param>
        /// <returns>Count of Pokémon that are now legal.</returns>
        public static int LegalizeBox(this SaveFile sav, int box)
        {
            if ((uint)box >= sav.BoxCount)
                return -1;

            var data = sav.GetBoxData(box);
            var ctr = sav.LegalizeAll(data);
            if (ctr > 0)
                sav.SetBoxData(data, box);

            return ctr;
        }

        /// <summary>
        /// Legalizes all <see cref="PKM"/> in all boxes.
        /// </summary>
        /// <param name="sav">Save File to legalize</param>
        /// <returns>Count of Pokémon that are now legal.</returns>
        public static int LegalizeBoxes(this SaveFile sav)
        {
            if (!sav.HasBox)
                return -1;

            var ctr = 0;
            for (int i = 0; i < sav.BoxCount; i++)
            {
                var result = sav.LegalizeBox(i);
                if (result < 0)
                    return result;

                ctr += result;
            }
            return ctr;
        }

        /// <summary>
        /// Legalizes all <see cref="PKM"/> in the provided <see cref="data"/>.
        /// </summary>
        /// <param name="sav">Save File context to legalize with</param>
        /// <param name="data">Data to legalize</param>
        /// <returns>Count of Pokémon that are now legal.</returns>
        public static int LegalizeAll(this SaveFile sav, IList<PKM> data)
        {
            var ctr = 0;
            for (int i = 0; i < data.Count; i++)
            {
                var pk = data[i];
                if (pk.Species <= 0 || new LegalityAnalysis(pk).Valid)
                    continue;

                var result = sav.Legalize(pk);
                result.Heal();
                if (!new LegalityAnalysis(result).Valid)
                    continue; // failed to legalize

                data[i] = result;
                ctr++;
            }

            return ctr;
        }
        public static PKM[] GetSixRandomMons(this SaveFile sav)
        {
            var RandomTeam = new List<PKM>();
            var selectedSpecies = new HashSet<ushort>();
            var rng = new Random();

            while (RandomTeam.Count < 6)
            {
                var spec = (ushort)rng.Next(sav.MaxSpeciesID);

                if (selectedSpecies.Contains(spec))
                    continue;

                var rough = EntityBlank.GetBlank(sav);
                rough.Species = spec;
                rough.Gender = rough.GetSaneGender();

                if (!sav.Personal.IsSpeciesInGame(rough.Species))
                    continue;

                if (APILegality.RandTypes.Length > 0 && (!APILegality.RandTypes.Contains((MoveType)rough.PersonalInfo.Type1) || !APILegality.RandTypes.Contains((MoveType)rough.PersonalInfo.Type2)))
                    continue;

                var formnumb = sav.Personal[rough.Species].FormCount;
                if (formnumb == 1)
                    formnumb = (byte)FormConverter.GetFormList(rough.Species, GameInfo.Strings.types, GameInfo.Strings.forms, GameInfo.GenderSymbolUnicode, sav.Context).Length;

                do
                {
                    if (formnumb == 0) break;
                    rough.Form = (byte)rng.Next(formnumb);
                }
                while (!sav.Personal.IsPresentInGame(rough.Species, rough.Form) || FormInfo.IsLordForm(rough.Species, rough.Form, sav.Context) || FormInfo.IsBattleOnlyForm(rough.Species, rough.Form, sav.Generation) || FormInfo.IsFusedForm(rough.Species, rough.Form, sav.Generation) || (FormInfo.IsTotemForm(rough.Species, rough.Form) && sav.Context is not EntityContext.Gen7));

                if (rough.Species is ((ushort)Species.Meowstic) or ((ushort)Species.Indeedee))
                {
                    rough.Gender = rough.Form;
                    rough.Form = (byte)rough.Gender;
                }

                var item = GetFormSpecificItem((int)sav.Version, rough.Species, rough.Form);
                if (item is not null)
                    rough.HeldItem = (int)item;

                if (rough.Species == (ushort)Species.Keldeo && rough.Form == 1)
                    rough.Move1 = (ushort)Move.SecretSword;

                if (GetIsFormInvalid(rough, sav, rough.Form))
                    continue;

                try
                {
                    var goodset = new SmogonSetList(rough);
                    if (goodset.Valid && goodset.Sets.Count != 0)
                    {
                        var checknull = sav.GetLegalFromSet(goodset.Sets[0]);
                        if (checknull.Status != LegalizationResult.Regenerated)
                            continue;
                        RandomTeam.Add(checknull.Created);
                        selectedSpecies.Add(rough.Species);
                        continue;
                    }
                }
                catch (Exception) { }

                var showstring = new ShowdownSet(rough).Text.Split('\r')[0];
                showstring += "\nLevel: 100\n";
                Span<int> ivs = stackalloc int[6];
                ivs.Clear();
                EffortValues.SetMax(ivs, rough);
                showstring += $"EVs: {ivs[0]} HP / {ivs[1]} Atk / {ivs[2]} Def / {ivs[3]} SpA / {ivs[4]} SpD / {ivs[5]} Spe\n";
                var m = new ushort[4];
                rough.GetMoveSet(m, true);
                showstring += $"- {GameInfo.MoveDataSource.First(z => z.Value == m[0]).Text}\n- {GameInfo.MoveDataSource.First(z => z.Value == m[1]).Text}\n- {GameInfo.MoveDataSource.First(z => z.Value == m[2]).Text}\n- {GameInfo.MoveDataSource.First(z => z.Value == m[3]).Text}";
                showstring += "\n\n";
                var nullcheck = sav.GetLegalFromSet(new ShowdownSet(showstring));
                if (nullcheck.Status != LegalizationResult.Regenerated)
                    continue;
                RandomTeam.Add(nullcheck.Created);
                selectedSpecies.Add(rough.Species);
            }

            return RandomTeam.ToArray();
        }
    }
}
