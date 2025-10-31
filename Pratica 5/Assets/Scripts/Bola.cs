using UnityEngine;
using TMPro;
using System.Globalization;

public class Bola : MonoBehaviour
{
    [Header("Configurações de Jogo")]
    public float velocidade = 7.0f;   // Velocidade base da bola
    public float fatorDesvio = 3.0f; // Quanto influencia o ponto de contato no ângulo
    public int pontosParaVencer = 5;

    [Header("Pontuação do Jogo")]
    public int pontoA = 0;
    public int pontoB = 0;
    
    [Header("UI")]
    public TextMeshProUGUI textoPontoA;
    public TextMeshProUGUI textoPontoB;
    public TextMeshProUGUI textoVitoriaA;
    public TextMeshProUGUI textoVitoriaB;
    
    private Rigidbody2D rigidBody;
    private UdpClientFour udpClient;
    private bool bolaLancada = false;

    void Start()
    {
        rigidBody = GetComponent<Rigidbody2D>();
        udpClient = FindObjectOfType<UdpClientFour>();

        if (udpClient != null && udpClient.myId == 1)
        {
            Invoke(nameof(LancarBola), 1.0f);
        }
        AtualizarPlacar();
    }
    void FixedUpdate()
    {
        if (udpClient == null) return;

        if (udpClient.myId == 1)
        {
            if (!bolaLancada)
            {
                bolaLancada = true;
                Invoke(nameof(LancarBola), 1.0f);
            }
            string msg = $"BALL:{transform.position.x.ToString(CultureInfo.InvariantCulture)};" + 
                         $"{transform.position.y.ToString(CultureInfo.InvariantCulture)}";
            udpClient.SendUdpMessage(msg);
        }
    }
    void LancarBola()
    {
        float dirX = Random.Range(0, 2) == 0 ? -1 : 1;
        float dirY = Random.Range(-0.5f, 0.5f); // inicia com pequeno ângulo
        rigidBody.linearVelocity = new Vector2(dirX, dirY).normalized * velocidade;
        bolaLancada = true;
    }
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (udpClient == null) return;

        if (collision.gameObject.CompareTag("Raquete"))
        {
            // Pega o ponto de contato
            float posYbola = transform.position.y;
            float posYraquete = collision.transform.position.y;
            float alturaRaquete = collision.collider.bounds.size.y;

            // Calcula diferença (normalizado entre -1 e 1)
            float diferenca = (posYbola - posYraquete) / (alturaRaquete / 2.0f);
            // Direção X mantém, Y é baseado na diferença
            Vector2 direction = new Vector2(Mathf.Sign(rigidBody.linearVelocity.x), diferenca * fatorDesvio);
            rigidBody.linearVelocity = direction.normalized * velocidade;
        }
        else if (collision.gameObject.CompareTag("Gol1"))
        {
            pontoB++;
            AtualizarPlacar();
            ResetarBola();
        }
        else if (collision.gameObject.CompareTag("Gol2"))
        {
            pontoA++;
            AtualizarPlacar();
            ResetarBola();
        }
    }
    public void AtualizarPlacar()
    {
        textoPontoA.text = $"Pontos: {pontoA}";
        textoPontoB.text = $"Pontos: {pontoB}";
    }
    private void ResetarBola()
    {
        rigidBody.linearVelocity = Vector2.zero;
        transform.position = Vector3.zero;
        //bolaLancada = false;
        
        if (pontoA >= pontosParaVencer || pontoB >= pontosParaVencer)
        {
            FimDeJogo();
        }
        else if (udpClient.myId == 1)
        {
            Invoke(nameof(LancarBola), 0.25f);
            string msg = "SCORE: {pontoA};{pontoB}";
            udpClient.SendUdpMessage(msg);
        }
    }
    private void FimDeJogo()
    {
        rigidBody.linearVelocity = Vector2.zero;
        transform.position = Vector3.zero;
        
        if (pontoA >= pontosParaVencer)
        {
            textoVitoriaA.gameObject.SetActive(true);
        }
        else 
        {
            textoVitoriaB.gameObject.SetActive(true);
        }
    }
}
