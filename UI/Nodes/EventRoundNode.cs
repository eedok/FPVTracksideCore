﻿using Composition.Nodes;
using RaceLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Composition;
using Composition.Input;
using Tools;
using Composition.Layers;
using Microsoft.Xna.Framework.Input;
using System.ComponentModel;

namespace UI.Nodes
{
    public class EventRoundNode : EventXNode
    {
        public IEnumerable<EventRaceNode> RaceNodes { get { return contentContainer.Children.OfType<EventRaceNode>(); } }

        public ImageButtonNode MenuButton { get; private set; }

        public event RoundDelegate FillRound;
        public event RoundDelegate PastePilot;

        public event Action<EventTypes, Round> SetRaceTypes;

        public event Action<IEnumerable<Race>> MatchChannels;

        public IEnumerable<Race> Races { get { return RaceNodes.Select(r => r.Race); } }

        private bool canFill;
        private bool canPasteAll;
        private bool canRemove;
        private bool hasRace;

        private Node instructionNode;

        public event System.Action NeedFullRefresh;

        public RoundsNode RoundsNode { get; private set; }

        public EventRoundNode(RoundsNode roundsNode, Round round)
            :base(roundsNode.EventManager, round)
        {
            RoundsNode = roundsNode;

            MenuButton = new ImageButtonNode(@"img\settings.png", Color.Transparent, Theme.Current.Hover.XNA, Theme.Current.Rounds.Text.XNA);
            MenuButton.OnClick += (mie) => { ShowMenu(mie, MenuButton.Bounds.Location); };
            buttonContainer.AddChild(MenuButton, 0);

            instructionNode = new Node();
            instructionNode.RelativeBounds = new RectangleF(0.1f, 0.3f, 0.8f, 0.2f);

            string instructionText;

            switch (round.RoundType)
            {
                case Round.RoundTypes.DoubleElimination:
                case Round.RoundTypes.Final:
                    instructionText = "Races will be automatically " +
                                      "\nadded as more results come in.";
                    break;
                default:
                    instructionText = "Right click here to add a races." +
                                      "\nDrag and drop pilots to fill the race." +
                                      "\nAuto-Fill to generate a new round" +
                                      "\nPaste pilots from clipboard";
                    break;
            }

            TextNode instructions = new TextNode(instructionText, Theme.Current.Rounds.Text.XNA);
            instructions.Alignment = RectangleAlignment.Center;
            instructionNode.AddChild(instructions);

            AddChild(instructionNode);

            EventManager.OnEventChange += Refresh;
            EventManager.OnPilotRefresh += Refresh;
            EventManager.RaceManager.OnRaceReset += RaceManager_OnRaceReset;
            EventManager.RaceManager.OnPilotAdded += OnCurrentRacePilotChange;
            EventManager.RaceManager.OnPilotRemoved += OnCurrentRacePilotChange;

            UpdateTitle();
        }

        private void OnCurrentRacePilotChange(PilotChannel pilot)
        {
            if (Races.Contains(EventManager.RaceManager.CurrentRace))
            {
                Refresh();
            }
        }

        public override void Dispose()
        {
            EventManager.OnEventChange -= Refresh;
            EventManager.OnPilotRefresh -= Refresh;
            EventManager.RaceManager.OnRaceReset -= RaceManager_OnRaceReset;

            base.Dispose();
        }

        private void RaceManager_OnRaceReset(Race race)
        {
            UpdateButtons();

            if (Races.Contains(race))
            {
                Refresh();
            }
        }

        public void Refresh()
        {
            Refresh(false);
        }

        public void Refresh(bool full)
        {
            if (full)
            {
                NeedFullRefresh?.Invoke();
            }
            else
            {
                Race[] races = RaceNodes.Select(rn => rn.Race).ToArray();
                SetRaces(races);
            }
        }

        private void UpdateTitle()
        {
            if (Round.RoundType != Round.RoundTypes.Round)
            {
                string a = Round.RoundType.ToString();
                SetSubHeading(a.CamelCaseToHuman());
            }
            else
            {
                var sf = EventManager.RoundManager.SheetFormatManager.GetRoundSheetFormat(Round);
                if (sf != null)
                {
                    SetSubHeading(sf.Name);
                }
                else
                {
                    SetSubHeading("");
                }
            }
        }

        public void SetRaces(IEnumerable<Race> races)
        {
            hasRace = races.Any();

            UpdateTitle();

            instructionNode.Visible = !races.Any();

            foreach (Race race in races)
            {
                EventRaceNode rn = RaceNodes.FirstOrDefault(ran => ran.Race == race);
                if (rn == null)
                {
                    rn = new EventRaceNode(EventManager, race);
                    if (RoundsNode.RacesPerColumn < 3)
                    {
                        rn.AspectRatio = EventRaceNode.StandardAspectRatio * 2 / 3.0f;
                    }

                    rn.NeedRefresh += () => { Refresh(); };
                    rn.NeedFullRefresh += () => { Refresh(true); };
                    contentContainer.AddChild(rn);
                }

                // Sync the in memory round objects..
                race.Round = Round;
            }

            foreach (EventRaceNode rn in RaceNodes.ToArray())
            {
                if (!races.Contains(rn.Race))
                {
                    rn.Dispose();
                }

                rn.NeedsInit = true;
            }

            int racesPerColumn = RoundsNode.RacesPerColumn;

            int columns = (int)Math.Ceiling(races.Count() / (float)racesPerColumn);
            if (columns == 0)
                columns = 1;

            float width = 1.0f / columns;

            int column = 0;

            List<EventRaceNode> current = new List<EventRaceNode>();
            foreach (EventRaceNode rn in RaceNodes.OrderBy(rn => rn.Race.RaceNumber))
            {
                current.Add(rn);
                if (current.Count == racesPerColumn)
                {
                    float leftAlign = column * width;
                    MakeColumns(current, racesPerColumn, leftAlign, width);
                    column++;
                    current.Clear();
                }
            }

            if (current.Any())
            {
                float leftAlign = column * width;
                MakeColumns(current, racesPerColumn, leftAlign, width);
            }

            AspectRatio = 0.4f * columns;

            SetHeading(RaceStringFormatter.Instance.RoundToString(Round));
            UpdateButtons();
            RequestLayout();
        }

        private void ShowMenu(MouseInputEvent mie, Point position)
        {
            var lines = PlatformTools.Clipboard.GetLines();
            int pastePilotCount = EventManager.GetPilotsFromLines(lines, true).Count();

            MouseMenu mm = new MouseMenu(this);

            mm.AddItem("Add Race", AddRace);

            if (pastePilotCount > 0)
            {
                mm.AddItem("Paste Race", PasteRace);
            }

            if (canFill)
            {
                mm.AddItem("Auto-Fill Round", () => { FillRound?.Invoke(Round); });
            }

            if (!hasRace)
            {
                AddFormatMenu(mm, "Set Format");
            }

            if (canPasteAll && pastePilotCount > EventManager.Channels.Length)
            {
                mm.AddItem("Paste Pilots", () => { PastePilot?.Invoke(Round); });
            }

            if (Races.Any())
            {
                mm.AddItem("Set rounds channels back to pilots list", () => { MatchChannels?.Invoke(Races); });
            }

            mm.AddItem("Edit Round", EditRound);

            MouseMenu typeMenu = mm.AddSubmenu("Set Type");

            foreach (EventTypes t in Event.GetEventTypes())
            {
                EventTypes typee = t;

                string typeString = RaceStringFormatter.Instance.GetEventTypeText(typee);

                typeMenu.AddItem(typeString, () => { SetType(typee, Round); });
            }

            var sheet = EventManager.RoundManager.SheetFormatManager.GetRoundSheetFormat(Round);
            if (sheet != null)
            {
                mm.AddItem("View Sheet Contents", () =>
                {
                    BaseGame baseGame = CompositorLayer.Game as BaseGame;
                    baseGame.ShowNewWindow(new SheetNode(sheet));
                });

                mm.AddItem("Export Sheet Contents", () =>
                {
                    string filename = PlatformTools.SaveFileDialog("Export Sheet Contents", "XLSX|*.xlsx");
                    if (!string.IsNullOrEmpty(filename))
                    {
                        sheet.Save(filename);
                    }
                });
            }


            if (hasRace)
            {
                if (EventManager.RaceManager.TimingSystemManager.HasDummyTiming)
                {
                    mm.AddBlank();
                    mm.AddItem("Generate Dummy Results", () =>
                    {
                        GenerateDummyResults();
                    });
                }
            }

            mm.Show(position - mie.Translation);
        }

        private void SetType(EventTypes type, Round round)
        {
            SetRaceTypes?.Invoke(type, Round);
            contentContainer.ClearDisposeChildren();
            Refresh(true);
        }

        private void GenerateDummyResults()
        {
            var timing = EventManager.RaceManager.TimingSystemManager.PrimeSystems.OfType<Timing.DummyTimingSystem>().FirstOrDefault();
            foreach (Race race in EventManager.RaceManager.GetRaces(Round))
            {
                EventManager.RaceManager.GenerateResults(timing, race);
            }
            Refresh();
        }

        private void PasteRace()
        {
            var lines = PlatformTools.Clipboard.GetLines();
            IEnumerable<Tuple<Pilot, Channel>> pilotChannels = EventManager.GetPilotsFromLines(lines, true);
            if (pilotChannels.Any())
            {
                Race race = EventManager.RaceManager.AddRaceToRound(Round);

                using (Database db = new Database())
                {
                    foreach (var kvp in pilotChannels)
                    {
                        race.SetPilot(db, kvp.Item2, kvp.Item1);
                    }
                }
            }
            Refresh(true);
        }

        private void EditRound()
        {
            ObjectEditorNode<Round> editor = new ObjectEditorNode<Round>(Round);
            GetLayer<PopupLayer>().Popup(editor);
            editor.OnOK += (r) => 
            { 
                if (editor.Selected != null)
                {
                    using (Database db = new Database())
                    {
                        db.Rounds.Upsert(editor.Selected);
                    }
                    Refresh(true);
                }
            };
        }

        public override bool OnMouseInput(MouseInputEvent mouseInputEvent)
        {
            if (base.OnMouseInput(mouseInputEvent))
            {
                return true;
            }

            if (mouseInputEvent.Button == MouseButtons.Right && mouseInputEvent.ButtonState == ButtonStates.Released)
            {
                ShowMenu(mouseInputEvent, mouseInputEvent.Position);
                return true;
            }

            if (mouseInputEvent.Button == MouseButtons.Left && mouseInputEvent.ButtonState == ButtonStates.Pressed)
            {
                MouseInputEvent translated = Translate(mouseInputEvent);
                if (heading.Contains(translated.Position))
                {
                    GetLayer<DragLayer>()?.RegisterDrag(this, mouseInputEvent);
                }
            }

            return false;
        }

        protected override void UpdateButtons()
        {
            base.UpdateButtons();
            if (MenuButton != null)
                MenuButton.Scale(0.6f);

            canRemove = !Races.Any(r => r.Ended) && !(EventManager.Event.Rounds.Count == 1 && !Races.Any());
            canPasteAll = !Races.Any(r => r.Ended);

            canFill = !EventManager.RoundManager.DoesRoundHaveAllPilots(Round);
            canAddLapCount = true;
            canSum = true;
            canClone = true;
            canAddTimes = true;
            canAddFinal = Round.RoundType != Round.RoundTypes.Final;
            canAddRace = true;

            RemoveRoundButton.Visible = canRemove;
        }

        public override IEnumerable<Pilot> GetOrderedPilots()
        {
            foreach (Race race in Races.OrderBy(r => r.RaceOrder))
            {
                foreach (Pilot pilot in race.Pilots)
                {
                    yield return pilot;
                }
            }
        }

        public override bool OnDrop(MouseInputEvent mouseInputEvent, Node node)
        {
            MouseInputEvent translated = Translate(mouseInputEvent);

            if (node.Contains(translated.Position))
            {
                if (node.ParentChain.Contains(this))
                {
                    return false;
                }
            }

            EventRaceNode draggedRaceNode = node as EventRaceNode;
            if (draggedRaceNode != null)
            {
                Race draggedRace = draggedRaceNode.Race;
                if (draggedRace != null)
                {
                    using (Database db = new Database())
                    {
                        draggedRace.Round = Round;

                        bool found = false;
                        int number = 1;

                        foreach (EventRaceNode racenode in RaceNodes.Except(new EventRaceNode[] { draggedRaceNode }).OrderBy(r => r.Race.RaceNumber))
                        {

                            if (racenode.Bounds.Bottom > translated.Position.Y 
                             && racenode.Bounds.Right > translated.Position.X
                             && racenode.Bounds.Left < translated.Position.X 
                             && !found)
                            {
                                found = true;
                                draggedRace.RaceNumber = number;
                                number++;

                                racenode.Race.RaceNumber = number;
                                number++;
                            }
                            else
                            {
                                racenode.Race.RaceNumber = number;
                                number++;
                            }
                        }

                        if (!found)
                        {
                            draggedRace.RaceNumber = number;
                        }

                        Race[] races = RaceNodes.Select(rn => rn.Race).ToArray();

                        db.Races.Update(races);
                        db.Races.Update(draggedRace);
                    }
                }
                contentContainer.ClearDisposeChildren();

                NeedFullRefresh?.Invoke();
            }

            return base.OnDrop(mouseInputEvent, node);
        }
    }
}
