using UnityEngine;
using TMPro;
using System.Globalization;

public class Bola : MonoBehaviour
{
    private Rigidbody2D rb;
    private UdpClientFour udpClient;
    private bool bolaLancada = false;

    [Header("Regras")]
    public int pontoA = 0;
    public int pontoB = 0;
    public float velocidade = 5f;   // Velocidade base da bola
    public float fatorDesvio = 2f; // Quanto influencia o ponto de contato no ângulo
    public int pontosParaVencer = 10;
    
    [Header("UI")]
    public TextMeshProUGUI textoPontoA;
    public TextMeshProUGUI textoPontoB;
    public TextMeshProUGUI textoVitoriaLocal;
    public TextMeshProUGUI textoVitoriaRemote;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        udpClient = FindObjectOfType<UdpClientFour>();

        if (udpClient != null && udpClient.myId == 2)
        {
            Invoke(nameof(LancarBola), 1f);
        }
    }
    void FixedUpdate()
    {
        if (udpClient == null) return;

        if (!bolaLancada && udpClient.myId == 1)
        {
            bolaLancada = true;
            Invoke(nameof(LancarBola), 1f);
        }
        if (udpClient.myId == 1)
        {
            string msg = $"BALL:{transform.position.x.ToString(CultureInfo.InvariantCulture)};" + 
                         $"{transform.position.y.ToString(CultureInfo.InvariantCulture)}";
            udpClient.SendUdpMessage(msg);
        }
    }
    void LancarBola()
    {
        float dirX = Random.Range(0, 2) == 0 ? -1 : 1;
        float dirY = Random.Range(-0.5f, 0.5f); // inicia com pequeno ângulo
        rb.linearVelocity = new Vector2(dirX, dirY).normalized * velocidade;
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
            Vector2 direcao = new Vector2(Mathf.Sign(rb.linearVelocity.x), diferenca * fatorDesvio);
            rb.linearVelocity = direcao.normalized * velocidade;
        }
        else if (collision.gameObject.CompareTag("Gol1"))
        {
            pontoB++;
            UpdateScore(pontoA, pontoB);
            ResetarBola();
        }
        else if (collision.gameObject.CompareTag("Gol2"))
        {
            pontoA++;
            UpdateScore(pontoA, pontoB);
            ResetarBola();
        }
    }
    public void UpdateScore(int a, int b)
    {
        pontoA = a;
        pontoB = b;
        textoPontoA.text = $"Pontos: {pontoA}";
        textoPontoB.text = $"Pontos: {pontoB}";
    }
    void ResetarBola()
    {
        transform.position = Vector3.zero;
        rb.linearVelocity = Vector2.zero;
        
        if (pontoA >= pontosParaVencer || pontoB >= pontosParaVencer)
        {
            FimDeJogo();
        }
        else if (udpClient.myId == 1)
        {
            Invoke(nameof(LancarBola), 1.0f);
            string msg = "SCORE:{pontoA};{pontoB}";
            udpClient.SendUdpMessage(msg);
        }
    }
    void FimDeJogo()
    {
        transform.position = Vector3.zero;
        rb.linearVelocity = Vector2.zero;
        
        if (pontoA >= pontosParaVencer)
        {
            textoVitoriaLocal.gameObject.SetActive(true);
        }
        else 
        {
            textoVitoriaRemote.gameObject.SetActive(true);
        }
    }
}
