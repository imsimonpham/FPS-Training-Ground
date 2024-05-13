using System.Collections;
using UnityEngine;
public class PlayerController : MonoBehaviour
{
    #region Variables
    public bool canMove { get; private set; } = true;

    [Header("Movement Params")]
    [SerializeField] private float _walkSpeed;
    [SerializeField] private float _sprintSpeed;
    [SerializeField] private float _crouchSpeed;
    [SerializeField] private float _slopeSpeed;
    private bool _canSprint;
    private bool _isMovingForward;
    private bool _isSprinting;

    [Header("Jump Params")]
    [SerializeField] private float _gravity;
    [SerializeField] private float _jumpForce;
    private bool _canJump;

    [Header("Crouch Params")]
    /*[SerializeField] private bool _toggleCrouch;*/
    [SerializeField] private float _crouchingHeight;
    [SerializeField] private float _standingHeight;
    [SerializeField] private float _timeToCrouch;
    [SerializeField] private Vector3 _crouchingCenter;
    [SerializeField] private Vector3 _standingCenter;
    private bool _isCrouching;
    private bool _duringCrouchingAnim;
    private bool _canCrouch;

    //Sliding Params
    private Vector3 _hitPointNormal;
    private bool _isSliding;


    [Header("Headbob Params")]
    [SerializeField] private float _walkBobSpeed;
    [SerializeField] private float _walkBobAmount;
    [SerializeField] private float _sprintBobSpeed;
    [SerializeField] private float _sprintBobAmount;
    [SerializeField] private float _crouchBobSpeed;
    [SerializeField] private float _crouchBobAmount;
    private float _defaultCamPosY;
    private float _headBobTimer;

    [Header("Look Params")]
    [SerializeField, Range(1, 10)] private float _lookSpeedX; 
    [SerializeField, Range(1, 10)] private float _lookSpeedY; 
    [SerializeField, Range(1, 100)] private float _upperLookLimit; 
    [SerializeField, Range(1, 100)] private float _lowerLookLimit;

    //Player
    [Header("Player")]
    [SerializeField] private float _currentSpeed;   
    private Camera _playerCam;
    private CharacterController _characterController;
    private PlayerInputActions _playerInput;
    [SerializeField] private bool _isGrounded;

    //Private values
    private Vector3 _moveDir;
    private Vector2 _currentInput;
    private float _deltaMouseValueMultiplier = 50;
    private float _rotX = 0;
    #endregion

    private void Awake()
    {
        //get components
        _playerCam = GetComponentInChildren<Camera>();
        _characterController = GetComponentInChildren<CharacterController>();

        //enable Player Input Action Map
        _playerInput = new PlayerInputActions();
        _playerInput.Enable();

        //lock and hide cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        //Camera
        _defaultCamPosY = _playerCam.transform.localPosition.y;
    }

    private void Update()
    {
        _isGrounded = _characterController.isGrounded;
        if (canMove)
        {
            HandleMovement();
            HandleLook();
            HandleJump();
            HandleCrouch();
            HandleHeadbob();
            HandleSliding();
            ApplyFinalMovement();
        }
    }

    #region Movement & Look
    private void HandleMovement()
    {
        Vector2 input = _playerInput.Player.Move.ReadValue<Vector2>();

        //Handle Sprinting - player can only sprint when moving forward and the sprint action is activated
        if (_playerInput.Player.Sprint.WasPressedThisFrame()){
           _canSprint = !_canSprint;
        }
        if (input.y > 0)
            _isMovingForward = true;
        else
        {
            _isMovingForward = false;
            _canSprint = false;
        }
              
        if(_canSprint && _isMovingForward)
            _isSprinting = true;
        else
            _isSprinting= false;

        //set and apply speed
        SetSpeed();
        _currentInput = new Vector2(input.y * _currentSpeed, input.x * _currentSpeed);

        //set movement direction
        float moveDirY = _moveDir.y;
        _moveDir = (transform.TransformDirection(Vector3.forward) * _currentInput.x) + (transform.TransformDirection(Vector3.right) * _currentInput.y);
        _moveDir.y = moveDirY;
    }

    private void SetSpeed()
    {
        if (_isSprinting)
            _currentSpeed = _sprintSpeed;
        else if (_isCrouching)
            _currentSpeed = _crouchSpeed;
        else
            _currentSpeed = _walkSpeed;
    }
    private void HandleLook()
    {
        Vector2 input = _playerInput.Player.Look.ReadValue<Vector2>();

        //set rotation X
        _rotX -= input.y/_deltaMouseValueMultiplier * _lookSpeedY;

        //limit rotation
        _rotX = Mathf.Clamp(_rotX, -_upperLookLimit, _upperLookLimit);

        //set the rotation of player's camera
        _playerCam.transform.localRotation = Quaternion.Euler(_rotX, 0, 0);

        //set the rotation of the player
        transform.rotation *= Quaternion.Euler(0, input.x/_deltaMouseValueMultiplier * _lookSpeedX, 0);
    }
    #endregion

    #region Jump and Crouch
    private void HandleJump()
    {
        _canJump = _characterController.isGrounded;
        if (_canJump && _playerInput.Player.Jump.WasPressedThisFrame())
            _moveDir.y = _jumpForce;
    }

    private void HandleCrouch()
    {
        if (!_duringCrouchingAnim && _characterController.isGrounded)
            _canCrouch = true;
        else 
            _canCrouch = false;

        if (_canCrouch && _playerInput.Player.Crouch.WasPressedThisFrame())
            StartCoroutine(CrouchStand());
    }
    private IEnumerator CrouchStand()
    {
        //check for ceiling within 1unit
        if (_isCrouching && Physics.Raycast(_playerCam.transform.position, Vector3.up, 1f))
            yield break;

        _duringCrouchingAnim = true;

        float timeElapsed = 0;
        float currentHeight = _characterController.height;
        Vector3 currentCenter = _characterController.center;
        float targetHeight = _isCrouching ? _standingHeight : _crouchingHeight;
        Vector3 targetCenter = _isCrouching ? _standingCenter : _crouchingCenter;

        while (timeElapsed < _timeToCrouch)
        {
            _characterController.height = Mathf.Lerp(currentHeight, targetHeight, timeElapsed / _timeToCrouch);
            _characterController.center = Vector3.Lerp(currentCenter, targetCenter, timeElapsed / _timeToCrouch);
            timeElapsed += Time.deltaTime;
            yield return null;
        }

        _characterController.height = targetHeight;
        _characterController.center = targetCenter;

        _isCrouching = !_isCrouching;

        _duringCrouchingAnim = false;
    }
    #endregion

    #region Headbob & Sliding
    private void HandleHeadbob()
    {
        if (!_characterController.isGrounded) { return; }
        if(Mathf.Abs(_moveDir.x) > 0.1f || Mathf.Abs(_moveDir.z) > 0.1f)
        {
            float headbobSpeed = (_isCrouching ? _crouchBobSpeed : _isSprinting ? _sprintBobSpeed : _walkBobSpeed);
            float headbobAmount = (_isCrouching ? _crouchBobAmount : _isSprinting ? _sprintBobAmount : _walkBobAmount);
            _headBobTimer += Time.deltaTime * headbobSpeed;
            _playerCam.transform.localPosition = new Vector3(_playerCam.transform.localPosition.x, _defaultCamPosY + Mathf.Sin(_headBobTimer) * headbobAmount, _playerCam.transform.localPosition.z);
        }
    }

    private void HandleSliding()
    {
        if (_characterController.isGrounded && Physics.Raycast(transform.position, Vector3.down, out RaycastHit slopHitInfo,2f))
        {
            _hitPointNormal = slopHitInfo.normal;
            _isSliding = (Vector3.Angle(_hitPointNormal, Vector3.up) > _characterController.slopeLimit);
        }else
            _isSliding = false;
    }
    #endregion



    private void ApplyFinalMovement()
    {
        if (!_characterController.isGrounded)
            _moveDir.y -= _gravity * Time.deltaTime;

        if(_isSliding)
            _moveDir += new Vector3(_hitPointNormal.x, -_hitPointNormal.y, _hitPointNormal.z) * _slopeSpeed;

        _characterController.Move(_moveDir * Time.deltaTime);
    }
}
