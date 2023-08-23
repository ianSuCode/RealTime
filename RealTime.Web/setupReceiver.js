const setupReceiver = (containerId, cbRecive) => {
  document.getElementById(containerId).innerHTML = `
    <div>
      <label style="color: brown; font-weight: bold">WS URL</label>
      <input value="ws://localhost:5050/ws" id="inputUrl" />
      <button id="btnConnect">connect</button>
      <p id="infoConnect" style="color: red">
        Try to connect WS server.
      </p>
    </div>
    <div>
      <label>topic</label>
      <input value="apple" id="inputTopic" />
      <button id="btnSubscribe" disabled>subscribe</button>
      <p id="infoSubscribe" style="color: red">
        WebSocket is not ready.
      </p>
    </div>`

  let ws
  btnConnect.addEventListener('click', (e) => {
    if (ws) {
      infoConnect.innerText = 'WebSocket connection already opened.'
      return
    }

    ws = new WebSocket(inputUrl.value)

    ws.addEventListener('open', (event) => {
      infoConnect.innerText = 'WebSocket connection opened.'
      infoConnect.style.color = 'green'
      infoSubscribe.innerText = 'Try to subscribe topic.'
      btnSubscribe.disabled = false

      e.target.disabled = true
    })

    ws.addEventListener('close', (event) => {
      infoConnect.innerText = 'WebSocket connection closed.'
    })

    ws.addEventListener('error', (event) => {
      infoConnect.innerText += `WebSocket error: ${event}`
    })

    ws.addEventListener('message', (event) => {
      cbRecive(event.data)
    })
  })

  btnSubscribe.addEventListener('click', () => {
    if (!ws) {
      infoSubscribe.innerText = 'Try to connect WS server before subscribing!.'
      return
    }
    infoSubscribe.innerHTML = `Subscribe to [<strong>${inputTopic.value}</strong>] now.`
    infoSubscribe.style.color = 'green'
    ws.send(`subscribe:${inputTopic.value}`)
  })
}
