using System.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using Liminal.SDK.VR;
using Liminal.SDK.VR.Input;
using Liminal.SDK.VR.Pointers;

/// <summary>
/// <see cref="Fire"/> Is used to control the player's laser and call the destroy interface on enemy objects via a spherical raycast.
/// </summary>
public class Fire 
    : MonoBehaviour
{
    public GameObject obj;
    public GameObject ExplosionEffect;
    public GameObject FireEffect;
    public GameObject earth;
    
    [Header("Laser Details")]
    public GameObject CannonObject;
    public Laser playerLaser;
    public Reticule TargetingReticule;
    public float ReticuleFillSpeed;

    private int _shotsFired;
    private int _enemiesKilled;
    private IVRInputDevice _inputDevice;
    private Transform _aim;
    private Coroutine EnergyRefillRoutine;
    private bool _wasTriggerHeld;
    private bool _loggedAimMissing;
    private GameObject[] Ex;
    private GameObject[] Gu;
    private AudioSource Gunfire;
    private AudioSource Explosion;

    public int GetShotsFired()
        => _shotsFired;
    public int GetEnemiesKilled()
        => _enemiesKilled;

    private void OnValidate()
    {
        Assert.IsNotNull(TargetingReticule, "TargetingReticule must not be null.");
    }

    private void Start()
    {
        Gunfire = GetComponent<AudioSource>();
        Explosion = GetComponent<AudioSource>();
        Ex = new GameObject[50];
        Gu = new GameObject[10];
        for (int i = 0; i < 50; i++)
        {
            Ex[i] = GameObject.Instantiate(ExplosionEffect, new Vector3(0, 0, 0), new Quaternion());
            Ex[i].SetActive(false);
        }
        for (int i = 0; i < 10; i++)
        {
            Gu[i] = GameObject.Instantiate(FireEffect, new Vector3(0, 0, 0), new Quaternion());
            Gu[i].SetActive(false);
        }

        playerLaser.LaserRend = Instantiate(playerLaser.LaserPrefab, new Vector3(0, 0, 0), new Quaternion());
        playerLaser.SetLaserVisuals();

        _inputDevice = VRDevice.Device?.PrimaryInputDevice;

        Debug.Log($"[Fire] Start: inputDevice={_inputDevice?.GetType().Name ?? "null"}, rayInteractor={(PlayerAim.GetRayInteractor() == null ? "null" : "ok")}");

        playerLaser.CurrentLaserCharge = playerLaser.MaxLaserCharge;
        TargetingReticule.FillSpeed = ReticuleFillSpeed;
    }

    private void Update()
    {
        var rayInteractor = PlayerAim.GetRayInteractor();
        if (rayInteractor == null)
        {
            if (!_loggedAimMissing)
            {
                Debug.LogWarning("[Fire] Blocked: no ray interactor available from LiminalControllerManager - FireLaser will not run");
                _loggedAimMissing = true;
            }
            return;
        }

        if (_loggedAimMissing)
        {
            Debug.Log($"[Fire] Ray interactor acquired: {rayInteractor.name}");
            _loggedAimMissing = false;
        }

        _aim = PlayerAim.GetAimOrigin(rayInteractor);

        if (_inputDevice == null)
            _inputDevice = VRDevice.Device?.PrimaryInputDevice;

        this.gameObject.transform.position = obj.transform.position;
        this.gameObject.transform.rotation = obj.transform.rotation;

        FireLaser(rayInteractor);

        CannonObject.transform.rotation = _aim.rotation;
    }

    private void FireLaser(UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor rayInteractor)
    {
        var sdkTriggerHeld = _inputDevice != null && _inputDevice.GetButton(VRButton.One);
        var interactorTriggerHeld = rayInteractor.logicalSelectState.active;
        var triggerHeld = sdkTriggerHeld || interactorTriggerHeld;

        if (triggerHeld && !_wasTriggerHeld)
            Debug.Log($"[Fire] Trigger pressed (sdk={sdkTriggerHeld}, interactor={interactorTriggerHeld}): charge={playerLaser.CurrentLaserCharge:F1}/{playerLaser.MaxLaserCharge:F1}, CanFire={playerLaser.CanFire}");
        _wasTriggerHeld = triggerHeld;

        if (triggerHeld && playerLaser.CurrentLaserCharge > 0f && playerLaser.CanFire)
        {
            Debug.Log($"[Fire] Shot fired #{_shotsFired + 1}, charge before drain={playerLaser.CurrentLaserCharge:F1}");
            playerLaser.CanFire = false;
            playerLaser.DrainEnergy();

            LaserRaycast();
            playerLaser.UpdateLaserVisual();

            if(!Gunfire.isPlaying)
            {
                Gunfire.Play();
            }

            StartCoroutine(playerLaser.FireCooldownCoro(_aim.position));

            _shotsFired++;
        }

        if (!triggerHeld)
        {
            playerLaser.ChargeLaser();
        }
        else if (playerLaser.CurrentLaserCharge <= 1f)
        {
            if (EnergyRefillRoutine != null)
                return;

            EnergyRefillRoutine = StartCoroutine(FreeEnergyCoro(playerLaser.LaserCooldownTime * 2));
        }
        else
        {
            if (EnergyRefillRoutine != null)
                StopCoroutine(EnergyRefillRoutine);
        }

        TargetingReticule.SetTargetFillAmount(playerLaser.NormalisedCharge);
    }

    private IEnumerator FreeEnergyCoro(float cooldownTime)
    {
        yield return new WaitForSeconds(cooldownTime * 2f);

        playerLaser.CurrentLaserCharge += playerLaser.LaserDrainSpeedCurve.Evaluate(playerLaser.NormalisedCharge);
        EnergyRefillRoutine = null;
    }

    private void LaserRaycast()
    {
        if (!Physics.SphereCast(_aim.position, playerLaser.LaserRadius, _aim.forward, out var hit,
            Mathf.Infinity))
        {
            Debug.Log("[Fire] Raycast: no hit");
            return;
        }

        Debug.Log($"[Fire] Raycast hit: {hit.collider.name}, IEnemy={(hit.collider.GetComponent<IEnemy>() != null)}");

        playerLaser.LaserRend.SetPosition(1, hit.point);

        var killableObject = hit.collider.GetComponent<IEnemy>();

        if (killableObject == null)
            return;

        killableObject.Kill();

        PlayerEffect(0, hit.transform.position);
        Explosion.Play();

        _enemiesKilled++;
    }

    private void PlayerEffect(int effect, Vector3 pos)
    {
        switch (effect)
        {
            case 0:

                for (int i = 0; i < 50; i++)
                {
                    if (Ex[i].activeInHierarchy)
                    {
                        Ex[i].SetActive(false);
                    }
                    else
                    {
                        Ex[i].transform.position = pos;
                        Ex[i].SetActive(true);
                        i = 50;


                    }
                }
                break;
            case 1:
                for (int i = 0; i < 10; i++)
                {
                    if (Gu[i].activeInHierarchy)
                    {
                        Gu[i].SetActive(false);
                    }
                    else
                    {
                        Gu[i].transform.position = pos;
                        Gu[i].SetActive(true);
                        i = 10;
                    }
                }

                break;

        }
    }
}

