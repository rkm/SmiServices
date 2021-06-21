﻿using IsIdentifiableReviewer.Out;
using Microservices.IsIdentifiable.Rules;
using Smi.Common.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;
using Terminal.Gui.Trees;

namespace IsIdentifiableReviewer.Views.Manager
{
    /// <summary>
    /// View allowing editing and viewing of all rules for both IsIdentifiable and IsIdentifiableReviewer
    /// </summary>
    class AllRulesManagerView : View, ITreeBuilder<object>
    {
        private const string Analyser = "Analyser Rules";
        private const string Reviewer = "Reviewer Rules";
        private readonly IsIdentifiableOptions _analyserOpts;
        private readonly IsIdentifiableReviewerOptions _reviewerOpts;
        private RuleDetailView detailView;
        private TreeView<object> treeView;


        public AllRulesManagerView(IsIdentifiableOptions analyserOpts , IsIdentifiableReviewerOptions reviewerOpts)
        {
            Width = Dim.Fill();
            Height = Dim.Fill();

            this._analyserOpts = analyserOpts;
            this._reviewerOpts = reviewerOpts;

            treeView = new TreeView<object>(this);
            treeView.Width = Dim.Percent(50);
            treeView.Height = Dim.Fill();
            treeView.AspectGetter = NodeAspectGetter;
            treeView.AddObject(Analyser);
            treeView.AddObject(Reviewer);
            Add(treeView);

            detailView = new RuleDetailView()
            {
                X = Pos.Right(treeView),
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };
            Add(detailView);

            treeView.SelectionChanged += Tv_SelectionChanged;
            treeView.ObjectActivated += Tv_ObjectActivated;
            treeView.KeyPress += Tv_KeyPress;
        }

        private void Tv_KeyPress(KeyEventEventArgs obj)
        {
            if(obj.KeyEvent.Key == Key.DeleteChar)
            {
                if(treeView.SelectedObject is ICustomRule rule)
                {
                    // TODO : Delete
                }
            }
        }

        private void Tv_ObjectActivated(ObjectActivatedEventArgs<object> obj)
        {
            if (obj.ActivatedObject is Exception ex)
            {
                MainWindow.ShowException("Exception Details", ex);
            }
        }

        private void Tv_SelectionChanged(object sender, SelectionChangedEventArgs<object> e)
        {
            if(e.NewValue is ICustomRule r)
            {
                detailView.SetupFor(r);
            }
            if (e.NewValue is OutBase rulesFile)
            {
                detailView.SetupFor(rulesFile,rulesFile.RulesFile);
            }

            if(e.NewValue is RuleSetFileNode rsf)
            {
                detailView.SetupFor(rsf,rsf.File);
            }
        }

        private string NodeAspectGetter(object toRender)
        {
            if(toRender is IsIdentifiableRule basicrule)
            {
                return basicrule.IfPattern;
            }

            if (toRender is SocketRule socketRule)
            {
                return socketRule.Host + ":" + socketRule.Port;
            }
            if (toRender is WhiteListRule ignoreRule)
            {
                return ignoreRule.IfPattern ?? ignoreRule.IfPartPattern;
            }

            if(toRender is OutBase outBase)
            {
                return outBase.RulesFile.Name;
            }

            return toRender.ToString();
        }

        public bool SupportsCanExpand => true;

        public IsIdentifiableOptions AnalyserOpts => _analyserOpts;

        public bool CanExpand(object toExpand)
        {
            // These are the things that cannot be expanded upon
            if (toExpand is Exception)
                return false;

            if (toExpand is ICustomRule)
                return false;

            //everything else can be expanded
            return true;
        }

        public IEnumerable<object> GetChildren(object forObject)
        {
            try
            {
                return GetChildrenImpl(forObject).ToArray();
            }
            catch (Exception ex)
            {
                // if there is an error getting children e.g. file doesn't exist, put the
                // Exception object directly into the tree
                return new object[] { ex };
            }
        }

        private IEnumerable<object> GetChildrenImpl(object forObject)
        {
            if(ReferenceEquals(forObject,Analyser))
            {
                if(!string.IsNullOrWhiteSpace(_analyserOpts.RulesDirectory))
                {
                    foreach (var f in Directory.GetFiles(_analyserOpts.RulesDirectory,"*.yaml"))
                    {
                        yield return new RuleSetFileNode(new FileInfo(f));
                    }
                }

                if (!string.IsNullOrWhiteSpace(_analyserOpts.RulesFile))
                {
                    yield return new RuleSetFileNode(new FileInfo(_analyserOpts.RulesFile));
                }
            }
            if (ReferenceEquals(forObject,Reviewer))
            {
                if(!string.IsNullOrWhiteSpace(_reviewerOpts.RedList))
                {
                    yield return new RowUpdater(new FileInfo(_reviewerOpts.RedList));
                }
                if (!string.IsNullOrWhiteSpace(_reviewerOpts.IgnoreList))
                {
                    yield return new IgnoreRuleGenerator(new FileInfo(_reviewerOpts.IgnoreList));
                }
            }

            if (forObject is RuleSetFileNode ruleSet)
            {
                yield return new RuleTypeNode(ruleSet, "BasicRules", ruleSet.GetRuleSet().BasicRules);
                yield return new RuleTypeNode(ruleSet, "SocketRules", ruleSet.GetRuleSet().SocketRules);
                yield return new RuleTypeNode(ruleSet, "WhiteListRules", ruleSet.GetRuleSet().WhiteListRules);
                yield return new RuleTypeNode(ruleSet, "ConsensusRules", ruleSet.GetRuleSet().ConsensusRules);                
            }

            if(forObject is RuleTypeNode ruleType)
            {
                foreach(var r in ruleType.Rules)
                {
                    yield return r;
                }
            }
        }
    }
}
