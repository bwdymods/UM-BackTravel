using System;
using System.Collections.Generic;
using System.Reflection;
using UnderModAPI;
using DG.Tweening;

namespace BackTravel
{
    public class BackTravelMod : Mod
    {
        public override void OnEntry()
        {
            Patcher.Patch(this, "Thor.WarpPopup:Initialize", "WarpPopup", null);
        }

        private static bool WarpPopup(Thor.WarpPopup __instance, object data, Thor.Entity owner)
        {
            Logger.Info("Intercepting warp menu to allow back-travel...");

            //snag some private fields via reflection helper
            var mListItems = Reflector.GetField<List<Thor.WarpListItem>>(__instance, "mListItems");
            var m_itemPrefab = Reflector.GetField<Thor.WarpListItem>(__instance, "m_itemPrefab");
            var m_container = Reflector.GetField<Thor.RadialLayoutGroup>(__instance, "m_container");
            var m_content = Reflector.GetField<UnityEngine.GameObject>(__instance, "m_content");
            var m_reminder = Reflector.GetField<UnityEngine.GameObject>(__instance, "m_reminder");
            //we are replacing the Initialize method, so let's do the variable initialization
            Reflector.SetField(__instance, "mTimer", 0f, typeof(Thor.Popup));
            Reflector.SetField(__instance, "mData", data, typeof(Thor.Popup));
            Reflector.SetField(__instance, "mOwner", owner, typeof(Thor.Popup));
            Reflector.SetField(__instance, "mPlayerID", ((owner != null) ? owner.PlayerID : 0), typeof(Thor.Popup));

            //and some other initialization
            __instance.Animator.Update(UnityEngine.Time.deltaTime);
            __instance.CanvasGroup.interactable = __instance.CanvasGroup.blocksRaycasts;
            if (__instance.InputContext != null) Thor.Game.Instance.InputSystem.AddContext(((owner != null) ? owner.PlayerID : 0), __instance.InputContext);
            Reflector.Invoke(__instance, "Layout");
            //typeof(Thor.WarpPopup).GetMethod("Layout", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, null);
            FMODUnity.RuntimeManager.PlayOneShot(Reflector.GetField<string>(__instance, "m_openAudio", typeof(Thor.Popup)));
            if (__instance.RectTransform.anchoredPosition.y < -200f)
            {
                __instance.Animator.SetInteger("state", 1);
                m_container.Invert = true;
            }
            m_container.transform.DORotate(UnityEngine.Vector3.forward * 360f, 0.25f, DG.Tweening.RotateMode.FastBeyond360);
            DOTween.To(() => m_container.Radius, delegate (float x)
            {
                m_container.Radius = x;
            }, m_container.Radius, 0.25f);
            m_container.Radius = 0f;

            //let's choose our destinations
            Thor.WarpListItem warpListItem = null;
            foreach (var map in Reflector.GetField<List<Thor.UpgradeData>>(__instance, "m_maps"))
            {
                if (map.IsDiscovered && (map.UserData == -1 || map.UserData > Thor.Game.Instance.Simulation.Zone.Data.ZoneNumber))
                {
                    warpListItem = ((warpListItem == null) ? m_itemPrefab : UnityEngine.Object.Instantiate(m_itemPrefab, m_container.transform));
                    warpListItem.Initialize(((owner != null) ? owner.PlayerID : 0), map);
                    mListItems.Add(warpListItem);
                } /* this is the mod */ else if (map.IsDiscovered && map.UserData < Thor.Game.Instance.Simulation.Zone.Data.ZoneNumber)
                {
                    //maybe do something different for these ones?
                    warpListItem = ((warpListItem == null) ? m_itemPrefab : UnityEngine.Object.Instantiate(m_itemPrefab, m_container.transform));
                    warpListItem.Initialize(((owner != null) ? owner.PlayerID : 0), map);
                    mListItems.Add(warpListItem);
                }
            }

            //do we have anywhere to go? handle accordingly.
            if (mListItems.Count > 0)
            {
                m_content.SetActive(value: true);
                m_reminder.SetActive(value: false);
            }
            else
            {
                m_content.SetActive(value: false);
                m_reminder.SetActive(value: true);
                UnityEngine.Object.Destroy(m_itemPrefab.gameObject);
            }

            //hook up the closed event delegate (these things are tricky in reflection)
            var m = typeof(Thor.WarpPopup).GetMethod("OnClosed", BindingFlags.NonPublic | BindingFlags.Instance);
            __instance.RegisterEvent(Thor.UIEvent.EventType.Closed, (Thor.Popup.EventHandler)Delegate.CreateDelegate(typeof(Thor.Popup.EventHandler), __instance, m));

            //return false to stop the original from running
            return false;
        }
    }
}
