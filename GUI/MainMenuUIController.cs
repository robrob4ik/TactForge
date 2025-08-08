using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using MoreMountains.Tools;
using MoreMountains.MMInterface;
using Sirenix.OdinInspector;

namespace OneBitRob.EnigmaEngine
{ 
	public enum LoadingSceneModes { Regular, Additive}
	
	[AddComponentMenu("Enigma Engine/GUI/Main Menu Screen")]
	public class MainMenuUIController : MonoBehaviour
	{
		[Title("Panels")]
		public GameObject BestiaryPanel;
		public GameObject ShopPanel;
		public GameObject MissionsPanel;
		public GameObject RankingsPanel;
		public GameObject SettingsPanel;
		
		[Title("Level")]
		public string NextLevel;
		public LoadingSceneModes LoadingSceneMode = LoadingSceneModes.Regular;
		public string LoadingSceneName = "";
		
		[Title("Fades")]
		[Tooltip("the duration of the fade from black at the start of the level")]
		public float FadeInDuration = 1f;
		[Tooltip("the duration of the fade to black at the end of the level")]
		public float FadeOutDuration = 1f;
		[Tooltip("the tween type to use to fade the startscreen in and out ")]
		public MMTweenType Tween = new MMTweenType(MMTween.MMTweenCurve.EaseInOutCubic);

		[Title("Sound Settings Bindings")]
		[Tooltip("The switch used to turn the music on or off")]
		public MMSwitch MusicSwitch;
		[Tooltip("The switch used to turn the SFX on or off")]
		public MMSwitch SfxSwitch;

		protected virtual void Awake()
		{	
			EnigmaGUIManager.Instance.SetHUDActive (false);
			MMFadeOutEvent.Trigger(FadeInDuration, Tween);
			Cursor.visible = true;
		}

		protected async void Start()
		{
			await Task.Delay(1);
			
			if (MusicSwitch != null)
			{
				MusicSwitch.CurrentSwitchState = MMSoundManager.Instance.settingsSo.Settings.MusicOn ? MMSwitch.SwitchStates.Right : MMSwitch.SwitchStates.Left;
				MusicSwitch.InitializeState ();
			}

			if (SfxSwitch != null)
			{
				SfxSwitch.CurrentSwitchState = MMSoundManager.Instance.settingsSo.Settings.SfxOn ? MMSwitch.SwitchStates.Right : MMSwitch.SwitchStates.Left;
				SfxSwitch.InitializeState ();
			}
		}

		public virtual void PlaytestButton()
		{
			MMFadeInEvent.Trigger(FadeOutDuration, Tween);
			EnigmaLevelManager.Instance.GotoLevel(NextLevel);
		}
		
		public virtual void BestiaryButton()
		{
			MMFadeInEvent.Trigger(FadeOutDuration, Tween);
			BestiaryPanel.SetActive(true);
		}
		
		public virtual void ShopButton()
		{
			MMFadeInEvent.Trigger(FadeOutDuration, Tween);
			ShopPanel.SetActive(true);
		}
		
		public virtual void MissionsButton()
		{
			MMFadeInEvent.Trigger(FadeOutDuration, Tween);
			MissionsPanel.SetActive(true);
		}
		
		public virtual void RankingsButton()
		{
			MMFadeInEvent.Trigger(FadeOutDuration, Tween);
			RankingsPanel.SetActive(true);
		}
		
		public virtual void SettingsButton()
		{
			MMFadeInEvent.Trigger(FadeOutDuration, Tween);
			SettingsPanel.SetActive(true);
		}

		public virtual void ClosePanelButton()
		{
			RankingsPanel.SetActive(false);
			SettingsPanel.SetActive(false);
			MissionsPanel.SetActive(false);
			ShopPanel.SetActive(false);
			BestiaryPanel.SetActive(false);
		}
	}
}