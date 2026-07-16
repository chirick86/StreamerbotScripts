/**
 * Configuration options
 */
const CONFIG = {
  // The title of the Channel Point Reward that will trigger the effect
  rewardTitle: 'Show Image',

  // The URL of the image to show
  imageUrl: 'https://streamer.bot/logo.png',

  // The number of seconds to show the image for
  seconds: 5,

  // The width and height of the image (css)
  width: 'auto',
  height: 'auto',
};

// Streamer.bot Event Listener
window.client.on('Twitch.RewardRedemption', (message) => {
  if ((message.data.reward.title || message.data.title) === CONFIG.rewardTitle) {
    showImage();
  }
});

function showImage() {
  // Create our image element
  const imgElement = document.createElement('img');

  // Modify the image element src to point at the configured image URL
  imgElement.src = CONFIG.imageUrl;

  // Update image element width and height
  imgElement.style.width = CONFIG.width;
  imgElement.style.height = CONFIG.height;
  imgElement.style.objectFit = 'cover';

  // Append the image element to the HTML document
  document.body.appendChild(imgElement);

  // Set a timeout to remove the image after the configured amount of time
  setTimeout(() => {
    imgElement.remove();
  }, CONFIG.seconds * 1000);
}
