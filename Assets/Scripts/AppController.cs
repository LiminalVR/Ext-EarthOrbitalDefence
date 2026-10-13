using System.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

/// <summary>
/// AppController is used to control the length of the experience and display how long until the game ends. It also tells <see cref="Fire"/> that users can fire/not fire, <see cref="SpawnSystem"/> to stop spawning, and <see cref="Reticule"/> to hide the crosshairs from view.
/// </summary>
public class AppController
    : MonoBehaviour
{
    public float MaxGameLength;
    public float CurrentTime;
    public float AudioFadeSpeed;
    public float ClearingWallGrowthSpeed;
    public Transform Earth;
    public Text TextPanel;
    public Fire FireController;
    public SpawnSystem SpawnSystem;
    public Reticule Crosshairs;
    public ScoreMenu ScoreMenu;

    // Register the actual normal SDK experience root, including dynamic host loading.
    private Transform _sourceLightingOwner;
    private void OnEnable()
    {
        var owner = GetComponentInParent<Liminal.SDK.Core.ExperienceApp>(true);
        _sourceLightingOwner = owner == null ? null : owner.transform;
        EarthOrbitalSourceLightingScope.RegisterExperience(_sourceLightingOwner);
    }
    private void OnDisable()
    {
        EarthOrbitalSourceLightingScope.UnregisterExperience(_sourceLightingOwner);
        _sourceLightingOwner = null;
    }

    private bool _timerActive;
    private bool _isEnded;

    public float CountdownTime 
        => MaxGameLength - CurrentTime;
    public bool SetActive(bool state)
        => _timerActive = state;
    public bool SetIsEnded(bool state)
        => _isEnded = state;

    public void OnValidate()
    {
        Assert.IsNotNull(TextPanel, "TextPanel must not be null!");
        Assert.IsNotNull(FireController, "FireController must not be null!");
        Assert.IsNotNull(Earth, "Earth must not be null!");
        Assert.IsNotNull(Crosshairs, "Crosshairs must not be null!");
    }

    public void StartTimer()
    {
        SetActive(true);
        StartCoroutine(TimerCoro());
    }

    private IEnumerator TimerCoro()
    {
        FireController.playerLaser.CanFire = true;

        while (CurrentTime < MaxGameLength && !_isEnded)
        {
            while (!_timerActive)
            {
                yield return new WaitForEndOfFrame();
            }

            CurrentTime += Time.deltaTime;

            var minutes = Mathf.Floor(CountdownTime / 60).ToString("00");
            var seconds = (CountdownTime % 60).ToString("00");

            TextPanel.text = $"{minutes}:{seconds}";

            yield return new WaitForEndOfFrame();
        }

        StartCoroutine(EndSequenceCoro());
    }

    private IEnumerator EndSequenceCoro()
    {
        SpawnSystem.Active = false;
        FireController.playerLaser.CanFire = false;
        Crosshairs.SetCrosshairVisibility(false);

        TextPanel.text = $"00:00";

        yield return ClearEnemiesCoro();
        ScoreMenu.gameObject.SetActive(true);

        yield return ScoreMenu.DisplayScoreCoro(FireController.GetShotsFired(), FireController.GetEnemiesKilled());

        yield return EndExperience.EndExperienceCoro(AudioFadeSpeed);
    }

    private IEnumerator ClearEnemiesCoro()
    {
        var clearingWall = new GameObject("Clearing Wall");
        clearingWall.transform.SetParent(Earth);
        clearingWall.transform.localPosition = Vector3.zero;
        clearingWall.transform.SetParent(null);
        clearingWall.AddComponent<SphereCollider>();

        var elapsedTime = 0f;

        while (elapsedTime < 5f)
        {
            clearingWall.transform.localScale += ClearingWallGrowthSpeed * Time.deltaTime * new Vector3(1, 1, 1);
            elapsedTime += Time.deltaTime;
            yield return new WaitForEndOfFrame();
        }
    }
}
