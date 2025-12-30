using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimEvents : MonoBehaviour
{

    public Player player;
    private List<GameObject> activeEffects = new List<GameObject>();

    void Start()
    {
        player = GetComponentInParent<Player>();
    }
    public void OnResetAnimation()
    {
        player.ResetAnim();
    }
    public void OnCombo() 
    {
        player.IsComboTimeWindow();
    }

    public void OnEnd() 
    {
        player.EndTimeCombo();
    }
    public void OnLast()
    {
        player.LastCombo();
    }
    public void OnDodge()
    {

    }
    public void OnFinish() 
    {
        player.EndCombo();
    }

    public void OnJumpStart() => player.animator.applyRootMotion = false;
    public void OnJumpEnd() => player.animator.applyRootMotion = true;

    public void CarryWeapon()
    {
        GameObject rightWeapon = player.WeapownTranfrom.gameObject;
        GameObject leftWeapon = player.shieldTranfrom.gameObject;


        if (player._isCombat)
        {
            if (player.iventory.rightHandSlots.item != player.iventory.Empty_Item)
                rightWeapon.SetActive(true);

            if (player.iventory.leftHandSlots.item != player.iventory.Empty_Item)
                leftWeapon.SetActive(true);
        }
        else
        {
            rightWeapon.SetActive(false);
            leftWeapon.SetActive(false);
        }

    }

    public void OnFinishWithCleanup(float delay)
    {
        player.EndCombo();
        StartCoroutine(CleanupRoutine(delay));
    }

    private IEnumerator CleanupRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        foreach (GameObject effect in activeEffects)
        {
            if (effect != null) Destroy(effect);
        }
        activeEffects.Clear();
    }

    public void PlayEffect(GameObject effectPrefab)
    {
        if (effectPrefab != null)
        {
            GameObject vfx = Instantiate(effectPrefab, transform.position, transform.rotation);

            vfx.transform.position = transform.TransformPoint(effectPrefab.transform.position);
            vfx.transform.rotation = transform.rotation * effectPrefab.transform.rotation;
            vfx.transform.localScale = effectPrefab.transform.localScale;

            activeEffects.Add(vfx);
        }
    }



    public void PlaySound(AudioClip clip)
    {
        if (clip != null)
        {
            AudioSource.PlayClipAtPoint(clip, player.transform.position);
        }
    }

    

    public void OnSlash()
    {
        // New Auto-Generated Method
    }
}

    

