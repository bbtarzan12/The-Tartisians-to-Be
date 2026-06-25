using System.Collections;
using NUnit.Framework;
using Tartisians.Core.Events;
using Tartisians.Gameplay.Events;
using Tartisians.Gameplay.Pickups;
using Tartisians.Gameplay.Progression;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tartisians.Tests.PlayMode
{
    public class XpGemTests
    {
        [UnityTest]
        public IEnumerator Gem_WithinRadius_MagnetsAndCollects_RaisesXp()
        {
            int xpAmount = 0;
            var binding = new EventBinding<XpCollectedEvent>(e => xpAmount += e.Amount);
            EventBus<XpCollectedEvent>.Register(binding);

            var player = new GameObject("Player");
            player.transform.position = Vector3.zero;

            var gemGo = new GameObject("Gem");
            gemGo.transform.position = new Vector3(2f, 0.5f, 0f);
            var gem = gemGo.AddComponent<XpGem>();

            bool collected = false;
            var stats = new RunStats { PickupRadius = 5f };
            gem.Configure(5, player.transform, stats, _ => collected = true);

            for (int i = 0; i < 60 && !collected; i++)
            {
                yield return null;
            }

            EventBus<XpCollectedEvent>.Deregister(binding);

            Assert.IsTrue(collected, "반경 내 젬은 자석으로 끌려와 수집돼야 한다.");
            Assert.AreEqual(5, xpAmount, "수집 시 XpCollectedEvent가 보상값으로 발행돼야 한다.");

            Object.Destroy(player);
            if (gemGo != null) Object.Destroy(gemGo);
        }

        [UnityTest]
        public IEnumerator Gem_OutsideRadius_DoesNotCollect()
        {
            var player = new GameObject("Player2");
            player.transform.position = Vector3.zero;

            var gemGo = new GameObject("Gem2");
            gemGo.transform.position = new Vector3(20f, 0.5f, 0f);
            var gem = gemGo.AddComponent<XpGem>();

            bool collected = false;
            var stats = new RunStats { PickupRadius = 2.5f };
            gem.Configure(5, player.transform, stats, _ => collected = true);

            for (int i = 0; i < 20; i++)
            {
                yield return null;
            }

            Assert.IsFalse(collected, "반경 밖 젬은 끌려오지 않아야 한다.");

            Object.Destroy(player);
            Object.Destroy(gemGo);
        }
    }
}
