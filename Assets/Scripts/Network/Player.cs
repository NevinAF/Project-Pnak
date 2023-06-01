using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using Pnak.Input;
using UnityEngine;

namespace Pnak
{
	public class Player : NetworkBehaviour
	{
		public static Player LocalPlayer { get; private set; }

		[SerializeField] private Transform _AimGraphic;

		[Tooltip("The character sprite renderer. Temporary until we have character prefabs.")]
		public SpriteRenderer CharacterRenderer;

		[Tooltip("The character sprite renderer. Temporary until we have character prefabs.")]
		public TMPro.TextMeshPro CharacterText;

		public CharacterData LoadingData;

		public LifetimeMod BehaviourModifier;
		public StaticTransformMod PositionAndScaleMod;
		public KinematicPositionMod KinematicMoveMod;
		public GameObject BehaviourPrefab;


		[Networked(OnChanged = nameof(OnCharacterTypeChanged))]
		public byte CharacterType { get; private set; }

		public bool PlayerLoaded => CharacterType != 0;
		public CharacterData CurrentCharacterData => PlayerLoaded ? GameManager.Instance.Characters[CharacterType - 1] : LoadingData;

		[Networked] private TickTimer reloadDelay { get; set; }
		[Networked] private TickTimer towerDelay { get; set; }
		[Networked] private float _MP { get; set; }
		[Networked(OnChanged = nameof(OnPilotChanged))]
		private bool _Piloting { get; set; }
		public float MPPercent => _MP / CurrentCharacterData.MP_Max;
		public float MP => _MP;

		public override void FixedUpdateNetwork()
		{
			if (!HasStateAuthority) return;

			if (GetInput(out NetworkInputData input))
			{
				if (!PlayerLoaded) return;

				_MP = Mathf.Clamp(_MP + CurrentCharacterData.MP_RegenerationRate * Runner.DeltaTime, 0.0f, CurrentCharacterData.MP_Max);

				if (input.CurrentInputMap == Input.InputMap.Menu) return;
				if (_Piloting) return;

				Vector2 movement = input.Movement * CurrentCharacterData.Speed;
				transform.position += (Vector3)movement * Runner.DeltaTime;

				float _rotation = input.AimAngle;

				if (reloadDelay.ExpiredOrNotRunning(Runner))
				{
					if (input.GetButtonDown(1))
					{
						reloadDelay = TickTimer.CreateFromSeconds(Runner, CurrentCharacterData.ReloadTime);
						LiteNetworkManager.CreateNetworkObjectContext(CurrentCharacterData.ProjectilePrefab, new TransformData {
							Position = transform.position,
							RotationAngle = _rotation,
						});
					}
				}

				if (towerDelay.ExpiredOrNotRunning(Runner))
				{
					if (input.GetButtonPressed(2))
					{
						towerDelay = TickTimer.CreateFromSeconds(Runner, CurrentCharacterData.TowerPlacementTime);
						Runner.Spawn(CurrentCharacterData.TowerPrefab, transform.position, Quaternion.identity, null, (runner, o) =>
						{
							o.GetComponent<Tower>().Init(_rotation);
						});
					}
				}

				if (Runner.Simulation.IsForward)
				{
					if (input.GetButtonPressed(3))
					{
						LiteNetworkManager.CreateNetworkObjectContext(out LiteNetworkedData data, BehaviourPrefab);

						BehaviourModifier.SetDefaults(ref data);
						BehaviourModifier.SetRuntime(ref data);
						LiteNetworkManager.AddModifier(in data);

						// KinematicMoveMod.SetDefaults(
						// 	data: ref data,
						// 	spawnPosition: transform.position,
						// 	velocity: input.AimDirection * 15,
						// 	acceleration: MathUtil.AngleToDirection(_rotation + 90) * 5f
						// );
						KinematicMoveMod.SetRuntime(ref data);
						LiteNetworkManager.AddModifier(in data);
					}
				}
			}
		}

		public override void Render()
		{
			if (!PlayerLoaded) return;
			if (!Object.HasInputAuthority) return;

			if (LevelUI.Exists)
			{
				float? reloadTime = reloadDelay.RemainingTime(Runner);
				LevelUI.Instance.ShootReloadBar.RawValueRange = new Vector2(0.0f, CurrentCharacterData.ReloadTime);
				LevelUI.Instance.ShootReloadBar.NormalizedValue = reloadTime.HasValue ? (1 - reloadTime.Value / CurrentCharacterData.ReloadTime) : 1.0f;
				float? towerTime = towerDelay.RemainingTime(Runner);
				LevelUI.Instance.TowerReloadBar.RawValueRange = new Vector2(0.0f, CurrentCharacterData.TowerPlacementTime);
				LevelUI.Instance.TowerReloadBar.NormalizedValue = towerTime.HasValue ? (1 - towerTime.Value / CurrentCharacterData.TowerPlacementTime) : 1.0f;
				LevelUI.Instance.MPBar.RawValueRange = new Vector2(0.0f, CurrentCharacterData.MP_Max);
				LevelUI.Instance.MPBar.NormalizedValue = MPPercent;
			}

			_AimGraphic.rotation = Quaternion.Euler(0.0f, 0.0f, Input.GameInput.Instance.InputData.AimAngle);
		}

		public override void Spawned()
		{
			if (!Object.HasInputAuthority)
			{
				_AimGraphic.gameObject.SetActive(false);
				return;
			}
			
			if (LocalPlayer != null)
			{
				Debug.LogError("Multiple local players detected!");
				return;
			}
			LocalPlayer = this;

			GameManager.Instance.SceneLoader.FinishedLoading();
		}

		[Rpc(RpcSources.All, RpcTargets.StateAuthority)]
		public void RPC_ChangeMP(float change) => _MP += change;


		[Rpc(RpcSources.All, RpcTargets.All)]
		public void RPC_SetCharacterType(byte characterType) => SetCharacterType(characterType);

		private void SetCharacterType(byte characterType)
		{
			UnityEngine.Debug.Log("Setting character type to " + characterType);
			if (Object?.HasStateAuthority ?? false)
			{
				MessageBox.Instance.RPC_ShowMessage("Player changed character to " + CurrentCharacterData.Name + "!");
			}

			CharacterType = characterType;
			reloadDelay = TickTimer.CreateFromSeconds(Runner, CurrentCharacterData.ReloadTime);
			towerDelay = TickTimer.CreateFromSeconds(Runner, CurrentCharacterData.TowerPlacementTime);
			_MP = Mathf.Min(_MP, CurrentCharacterData.MP_Max);
		}

		public static void OnCharacterTypeChanged(Changed<Player> changed) => changed.Behaviour.SetCharacterTypeVisuals();
		private void SetCharacterTypeVisuals()
		{
			CharacterRenderer.sprite = CurrentCharacterData.Sprite;
			CharacterText.text = CurrentCharacterData.Name;
			CharacterRenderer.transform.localScale = (Vector3)CurrentCharacterData.SpriteScale;
			CharacterRenderer.transform.localPosition = (Vector3)CurrentCharacterData.SpritePosition;
		}

		[Rpc(RpcSources.All, RpcTargets.All)]
		public void RPC_SetPilot(NetworkId towerId, PlayerRef playerRef)
		{
			if (Runner.TryFindObject(towerId, out NetworkObject tower))
			{
				tower.AssignInputAuthority(playerRef);
				transform.position = tower.transform.position;
				
			}
			
			_Piloting = true;
		}

		[Rpc(RpcSources.All, RpcTargets.All)]
		public void RPC_UnsetPilot(NetworkId tower)
		{
			if (Runner.TryFindObject(tower, out NetworkObject towerObj))
				towerObj.RemoveInputAuthority();

			_Piloting = false;
		}

		public static void OnPilotChanged(Changed<Player> changed) => changed.Behaviour.SetPilotVisuals();
		private void SetPilotVisuals()
		{
			CharacterRenderer.gameObject.SetActive(!_Piloting);
		}

#if UNITY_EDITOR
		/// <summary>
		/// Sets the character information so it doesn't need to be loaded on create. Also useful for previewing.
		/// </summary>
		private void OnValidate()
		{
			if (Application.isPlaying) return;

			if (LoadingData != null)
			{
				if (CharacterRenderer != null)
				{
					CharacterRenderer.sprite = LoadingData.Sprite;
					CharacterRenderer.transform.localScale = (Vector3)LoadingData.SpriteScale;
					CharacterRenderer.transform.localPosition = (Vector3)LoadingData.SpritePosition;
				}
				if (CharacterText != null)
					CharacterText.text = LoadingData.Name;
			}
		}
#endif
	}
}