using System;
using System.Reflection;
using NUnit.Framework;

namespace LTC.Tests
{
    public sealed class PetHungerPlayModeTests
    {
        const double Day = 24d * 60d * 60d;
        const double Full = 3d * Day;

        static object Call(string method, params object[] args)
        {
            Type type = Type.GetType("PetHungerService, Assembly-CSharp");
            Assert.That(type, Is.Not.Null);
            return type.GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, args);
        }

        static int Meat(double remaining) => (int)Call("CountMeat", remaining);
        static double Decayed(double remaining, DateTime saved, DateTime now) =>
            (double)Call("DecayedSeconds", remaining, saved, now);
        static double Fed(double remaining) => (double)Call("AfterFeeding", remaining);

        [Test]
        public void ThreeMeatsDisappearExactlyOnePerDay()
        {
            DateTime start = new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);
            Assert.That(Meat(Decayed(Full, start, start)), Is.EqualTo(3));
            Assert.That(Meat(Decayed(Full, start, start.AddDays(1))), Is.EqualTo(2));
            Assert.That(Meat(Decayed(Full, start, start.AddDays(2))), Is.EqualTo(1));
            Assert.That(Meat(Decayed(Full, start, start.AddDays(3))), Is.EqualTo(0));
        }

        [Test]
        public void FeedingRestoresOneMeatButNeverExceedsThree()
        {
            Assert.That(Meat(Fed(0)), Is.EqualTo(1));
            Assert.That(Fed(Full), Is.EqualTo(Full));
        }

        [Test]
        public void MovingClockBackwardDoesNotCreateExtraHunger()
        {
            DateTime saved = new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);
            Assert.That(Decayed(Day, saved, saved.AddHours(-1)), Is.EqualTo(Day));
        }
    }
}
