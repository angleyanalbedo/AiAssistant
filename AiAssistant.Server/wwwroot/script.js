const chatForm = document.getElementById('chatForm');
const userInput = document.getElementById('userInput');
const chatLog = document.getElementById('chatLog');
const submitButton = chatForm.querySelector('button[type="submit"]');

chatForm.addEventListener('submit', function (e) {
    e.preventDefault();
    sendMessage();
});

async function sendMessage() {
    const messageText = userInput.value.trim();
    if (!messageText) return;

    appendMessage(messageText, 'user');
    userInput.value = '';
    submitButton.disabled = true;

    const thinkingMessage = appendMessage('思考中...', 'ai');

    try {
        const response = await fetch('/api/chat', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ message: messageText }),
        });

        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(`HTTP error! status: ${response.status}. ${errorText}`);
        }

        const data = await response.json();
        thinkingMessage.querySelector('.message-content').textContent = data.reply;
    } catch (error) {
        thinkingMessage.querySelector('.message-content').textContent = `Error: ${error.message}`;
    } finally {
        submitButton.disabled = false;
        userInput.focus();
        chatLog.scrollTop = chatLog.scrollHeight;
    }
}

function appendMessage(text, sender) {
    const messageWrapper = document.createElement('div');
    messageWrapper.classList.add('message', `${sender}-message`);
    
    const messageContent = document.createElement('div');
    messageContent.classList.add('message-content');
    messageContent.textContent = text;
    
    messageWrapper.appendChild(messageContent);
    chatLog.appendChild(messageWrapper);
    chatLog.scrollTop = chatLog.scrollHeight;
    return messageWrapper;
}
