using System.Collections.Generic;
using UnityEngine;

public class FaceToCamera : MonoBehaviour
{
    [SerializeField] private SpriteRenderer sprRndr;
    [SerializeField] private GameObject obj_RotateObject;
    [SerializeField] private Animator anim;
    [SerializeField] private bool isFaceToPlayer = true;

    public bool isFaceYAxis = false;

    [Header("Sprites")]
    [SerializeField] private Sprite spr_Front;
    [SerializeField] private Sprite spr_FrontSide;
    [SerializeField] private Sprite spr_Side;
    [SerializeField] private Sprite spr_BackSide;
    [SerializeField] private Sprite spr_Back;

    [Header("AnimationController")]
    public RuntimeAnimatorController animController_Front;
    public RuntimeAnimatorController animController_FrontSide;
    public RuntimeAnimatorController animController_Side;
    public RuntimeAnimatorController animController_BackSide;
    public RuntimeAnimatorController animController_Back;

    private Camera mainCamera;
    private float angle;

    void Start()
    {
        mainCamera = Camera.main;
    }

    void LateUpdate()
    {
        if(isFaceToPlayer)
            UpdateCameraDirection();

        if (anim != null)
        {
            UpdateAnimation();
        }
        else
        {
            UpdateSingleSprite();
        }
    }

    private void UpdateCameraDirection()
    {
        if (mainCamera == null || obj_RotateObject == null) return;

        Vector3 targetDir = mainCamera.transform.position - obj_RotateObject.transform.position;

        if (!isFaceYAxis)
            targetDir.y = 0;

        if (targetDir != Vector3.zero)
        {
            obj_RotateObject.transform.rotation = Quaternion.LookRotation(targetDir, Vector3.up);
        }

        Vector3 dirToCamera = mainCamera.transform.position - transform.position;

        if(!isFaceYAxis)
            dirToCamera.y = 0;

        Vector3 baseForward = transform.forward;

        if (!isFaceYAxis)
            baseForward.y = 0;

        angle = Vector3.SignedAngle(baseForward, dirToCamera, Vector3.up);
    }

    private void UpdateSingleSprite()
    {
        if (angle > -22.5f && angle <= 22.5f)
        {
            // Front
            if (spr_Front == null) { return; }
            sprRndr.sprite = spr_Front;
            sprRndr.flipX = false;
        }
        else if (angle > 22.5f && angle <= 67.5f)
        {
            // Front-Right
            if (spr_FrontSide == null) { return; }
            sprRndr.sprite = spr_FrontSide;
            sprRndr.flipX = true;
        }
        else if (angle > 67.5f && angle <= 112.5f)
        {
            // Right Side
            if (spr_Side == null) { return; }
            sprRndr.sprite = spr_Side;
            sprRndr.flipX = true;
        }
        else if (angle > 112.5f && angle <= 157.5f)
        {
            // Back-Right
            if (spr_BackSide == null) { return; }
            sprRndr.sprite = spr_BackSide;
            sprRndr.flipX = true;
        }
        else if (angle > 157.5f || angle <= -157.5f)
        {
            // Back
            if (spr_Back == null) { return; }
            sprRndr.sprite = spr_Back;
            sprRndr.flipX = false;
        }
        else if (angle > -157.5f && angle <= -112.5f)
        {
            // Back-Left
            if (spr_BackSide == null) { return; }
            sprRndr.sprite = spr_BackSide;
            sprRndr.flipX = false;
        }
        else if (angle > -112.5f && angle <= -67.5f)
        {
            // Left Side
            if (spr_Side == null) { return; }
            sprRndr.sprite = spr_Side;
            sprRndr.flipX = false;
        }
        else // angle > -67.5f && angle <= -22.5f
        {
            // Front-Left
            if (spr_FrontSide == null) { return; }
            sprRndr.sprite = spr_FrontSide;
            sprRndr.flipX = false;
        }
    }

    private void UpdateAnimation()
    {
        if (angle > -22.5f && angle <= 22.5f)
        {
            // Front
            if (animController_Front == null) { return; }
            anim.runtimeAnimatorController = animController_Front;
            sprRndr.flipX = false;
        }
        else if (angle > 22.5f && angle <= 67.5f)
        {
            // Front-Right
            if (animController_FrontSide == null) { return; }
            anim.runtimeAnimatorController = animController_FrontSide;
            sprRndr.flipX = true;
        }
        else if (angle > 67.5f && angle <= 112.5f)
        {
            // Right Side
            if (animController_Side == null) { return; }
            anim.runtimeAnimatorController = animController_Side;
            sprRndr.flipX = true;
        }
        else if (angle > 112.5f && angle <= 157.5f)
        {
            // Back-Right
            if (animController_BackSide == null) { return; }
            anim.runtimeAnimatorController = animController_BackSide;
            sprRndr.flipX = true;
        }
        else if (angle > 157.5f || angle <= -157.5f)
        {
            // Back
            if (animController_Back == null) { return; }
            anim.runtimeAnimatorController = animController_Back;
            sprRndr.flipX = false;
        }
        else if (angle > -157.5f && angle <= -112.5f)
        {
            // Back-Left
            if (animController_BackSide == null) { return; }
            anim.runtimeAnimatorController = animController_BackSide;
            sprRndr.flipX = false;
        }
        else if (angle > -112.5f && angle <= -67.5f)
        {
            // Left Side
            if (animController_Side == null) { return; }
            anim.runtimeAnimatorController = animController_Side;
            sprRndr.flipX = false;
        }
        else // angle > -67.5f && angle <= -22.5f
        {
            // Front-Left
            if (animController_FrontSide == null) { return; }
            anim.runtimeAnimatorController = animController_FrontSide;
            sprRndr.flipX = false;
        }
    }
}