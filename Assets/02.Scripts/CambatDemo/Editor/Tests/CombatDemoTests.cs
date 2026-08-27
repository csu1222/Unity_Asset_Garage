#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AssetGarage.CombatDemo.Editor.Tests
{
    public sealed class CombatDemoTests
    {
        private ManualCombatBalanceConfig manual; private AutoCombatBalanceConfig autoConfig; private ResolutionBalanceConfig resolution;
        [SetUp] public void Setup(){manual=ScriptableObject.CreateInstance<ManualCombatBalanceConfig>();autoConfig=ScriptableObject.CreateInstance<AutoCombatBalanceConfig>();resolution=ScriptableObject.CreateInstance<ResolutionBalanceConfig>();}
        [TearDown] public void Cleanup(){Object.DestroyImmediate(manual);Object.DestroyImmediate(autoConfig);Object.DestroyImmediate(resolution);}

        [TestCase(0f,TimingGrade.Normal)][TestCase(.8499f,TimingGrade.Normal)][TestCase(.85f,TimingGrade.Great)][TestCase(.9249f,TimingGrade.Great)][TestCase(.925f,TimingGrade.Extreme)][TestCase(1f,TimingGrade.Extreme)]
        public void TimingBoundaries(float time,TimingGrade expected)=>Assert.That(TimingRules.Grade(time,false),Is.EqualTo(expected));
        [Test] public void ExpiredTimingFails()=>Assert.That(TimingRules.Grade(1,true),Is.EqualTo(TimingGrade.Failed));
        [Test] public void TimingExpirationSignalsOnce(){var controller=new ManualCombatTimingController(manual);int count=0;controller.Expired+=()=>count++;controller.Start();controller.Tick(2);controller.Tick(2);Assert.That(count,Is.EqualTo(1));Assert.That(controller.State.CurrentGrade,Is.EqualTo(TimingGrade.Failed));}
        [TestCase(CombatAction.Attack,CombatAction.Attack)][TestCase(CombatAction.Attack,CombatAction.Defense)][TestCase(CombatAction.Attack,CombatAction.Recovery)][TestCase(CombatAction.Defense,CombatAction.Attack)][TestCase(CombatAction.Defense,CombatAction.Defense)][TestCase(CombatAction.Defense,CombatAction.Recovery)][TestCase(CombatAction.Recovery,CombatAction.Attack)][TestCase(CombatAction.Recovery,CombatAction.Defense)][TestCase(CombatAction.Recovery,CombatAction.Recovery)]
        public void ManualMatrixResolves(CombatAction playerAction,CombatAction enemyAction){var p=new CombatStats(100,50,20,15,10);var e=new CombatStats(100,50,18,12,8);var result=new ManualCombatResolver(manual).Resolve(p,e,playerAction,enemyAction,TimingGrade.Normal,false);Assert.That(result.PlayerDamage+result.EnemyDamage+result.PlayerHeal+result.EnemyHeal+result.CounterDamage,Is.GreaterThanOrEqualTo(0));}
        [Test] public void ExtremeDefenseBlocksAndCounters(){var p=new CombatStats(100,100,20,20,10);var e=new CombatStats(100,100,30,10,10);var r=new ManualCombatResolver(manual).Resolve(p,e,CombatAction.Defense,CombatAction.Attack,TimingGrade.Extreme,false);Assert.That(r.PlayerDamage,Is.Zero);Assert.That(r.CounterDamage,Is.EqualTo(60));}
        [Test] public void NonExtremeDefenseHasMinimumDamage(){var p=new CombatStats(100,100,1,100000,1);var e=new CombatStats(100,100,1,1,1);var r=new ManualCombatResolver(manual).Resolve(p,e,CombatAction.Defense,CombatAction.Attack,TimingGrade.Normal,false);Assert.That(r.PlayerDamage,Is.EqualTo(1));}
        [Test] public void PressureThresholdAndReset(){var p=new PressureState();p.Record(TimingGrade.Normal);p.Record(TimingGrade.Normal);p.Record(TimingGrade.Failed);Assert.That(p.ShouldEmpower(3),Is.True);p.ConsumeEmpowerment();Assert.That(p.Value,Is.Zero);p.Record(TimingGrade.Normal);p.Record(TimingGrade.Normal);p.Record(TimingGrade.Great);Assert.That(p.Value,Is.Zero);}
        [Test] public void EqualPowerIsFiftyPercent(){var resolver=new AutoCombatResolver(autoConfig);Assert.That(resolver.WinProbability(100,100),Is.EqualTo(.5).Within(1e-9));}
        [Test] public void LowerCurrentHpLowersPower(){var resolver=new AutoCombatResolver(autoConfig);Assert.That(resolver.Power(new CombatStats(100,20,10,10,10)),Is.LessThan(resolver.Power(new CombatStats(100,100,10,10,10))));}
        [Test] public void DeterministicRollRepeats(){double a=DeterministicRoll.Value(1,"E","C",2,"AutoCombat");Assert.That(DeterministicRoll.Value(1,"E","C",2,"AutoCombat"),Is.EqualTo(a));}
        [Test] public void NegotiationUsesBaseAtEqualPower(){var auto=new AutoCombatResolver(autoConfig);var n=new NegotiationResolver(resolution,auto);var p=new CombatStats(100,100,10,10,10);Assert.That(n.Cost(p,p.Copy()),Is.EqualTo(500));}
        [Test] public void EscapeClampsAndRepeats(){var e=new EncounterData{EncounterId="x",CaravanId="c",CreatedTick=3};var resolver=new EscapeResolver(resolution);Assert.That(resolver.Chance(9),Is.EqualTo(1));var p=new CombatStats();Assert.That(resolver.Resolve(p,e,4).RandomRoll,Is.EqualTo(resolver.Resolve(p,e,4).RandomRoll));}
        [Test] public void QueueTimeoutsResolveInOrder(){var q=new CombatEncounterQueue();q.Enqueue(new EncounterData{EncounterId="A",RemainingWait=99});q.Enqueue(new EncounterData{EncounterId="B",RemainingWait=1});q.Enqueue(new EncounterData{EncounterId="C",RemainingWait=1});var order=new List<string>();q.Update(1,e=>order.Add(e.EncounterId));CollectionAssert.AreEqual(new[]{"B","C"},order);}
        [Test] public void OfflineCarriesCurrentHp(){var resolver=new AutoCombatResolver(autoConfig);var offline=new OfflineCombatResolver(resolver);var p=new CombatStats(100,100,200,200,200);var list=new[]{new EncounterData{EncounterId="B",OccurrenceTime=2,CreatedTick=2,Enemy=new CombatStats(1,1,1,1,1)},new EncounterData{EncounterId="A",OccurrenceTime=1,CreatedTick=1,Enemy=new CombatStats(1,1,1,1,1)}};var results=offline.Resolve(p,list,5);Assert.That(results[1].PlayerHPBefore,Is.EqualTo(results[0].PlayerHPAfter));}
        [Test] public void TimingViewChoiceDoesNotMutateState(){var controller=new ManualCombatTimingController(manual);controller.Start();controller.Tick(.75f);TimingState before=controller.State;TimingViewKind kind=TimingViewKind.RadialPin;Assert.That(kind,Is.EqualTo(TimingViewKind.RadialPin));Assert.That(controller.State.NormalizedTime,Is.EqualTo(before.NormalizedTime));}
        [Test] public void ConvergingGuideRadiiUseSharedThresholds(){float great=TimingPresentationMapping.Radius(145,45,.85f);float extreme=TimingPresentationMapping.Radius(145,45,.925f);Assert.That(great,Is.EqualTo(60).Within(.001f));Assert.That(extreme,Is.EqualTo(52.5f).Within(.001f));Assert.That(145,Is.GreaterThan(great));Assert.That(great,Is.GreaterThan(extreme));Assert.That(extreme,Is.GreaterThanOrEqualTo(45));}
        [Test] public void RadialGuideAnglesUseSharedThresholds(){Assert.That(TimingPresentationMapping.Angle(.85f),Is.EqualTo(306).Within(.001f));Assert.That(TimingPresentationMapping.Angle(.925f),Is.EqualTo(333).Within(.001f));}
        [Test] public void TimingStateCarriesConfiguredThresholds(){manual.GreatStart=.8f;manual.ExtremeStart=.9f;var controller=new ManualCombatTimingController(manual);controller.Start();Assert.That(controller.State.GreatStart,Is.EqualTo(.8f));Assert.That(controller.State.ExtremeStart,Is.EqualTo(.9f));}
    }
}
#endif
