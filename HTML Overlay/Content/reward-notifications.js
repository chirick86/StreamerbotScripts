const CONFIG = {
	defaultDurationMs: 6500,
	debug: true,
	ui: {
		imageSizePx: 500,
		titleFontSizePx: 26,
		rewardTagFontSizePx: 0,
		positionBottomPx: 220,
		tagColor: '#a970ff',
		textShadow: {
			offsetXPx: 1.5,
			offsetYPx: 1.5,
			blurPx: 1,
			opacity: 0.95,
		},
	},

	groups: [
		{
			name: 'Sport',
			rewards: ['Бицес', 'Планка', 'Приседания', 'Отжимания', 'Пресс'],
			image: 'sportik.gif',
			sound: 'sportik.wav',
			textTemplate: '{nickname} копается в шестерятах и извлекает {rewardName}',
			className: 'notification-card--chir',
		},
		{
			name: 'Gears',
			rewards: ['Водичка!', 'Ровнее спинку!', 'Запретить действие в игре или IRL'],
			image: 'gears.gif',
			sound: 'gears.wav',
			textTemplate: '{nickname} копается в шестерятах и извлекает {rewardName}',
			className: 'notification-card--chir',
		},
		{
			name: 'GiggleDeath',
			rewards: ['Посмеяться над смертью'],
			renderer: 'video',
			video: ['mario_game_over.webm', 'directed.webm', 'ds_you_died.webm', 'fatality.webm',
				'gta_potracheno.webm', 'gta_wasted.webm', 'skyrim_awake.webm'],
			className: 'notification-card--video',
		},
		{
			name: 'Distract',
			rewards: ['Отвлечь стримера'],
			renderer: 'video',
			video: ['mk2_toasty.webm', 'BSOD.webm', 'time_to_stop.webm', 'here_we_go_again.webm', 'xp_error.webm',
				'spongebob_a_few_moments_later.webm', 'flashbang.webm', 'subway_surf.webm', 'do_it.webm'],
			className: 'notification-card--video',
		},
	],


};

const container = ensureContainer();
injectStyles();

console.log('[reward-notifications] script loaded');

startRewardListener();

function startRewardListener() {
	let attached = false;

	const attach = () => {
		if (attached) return true;
		if (!window.client || typeof window.client.on !== 'function') return false;

		window.client.on('Twitch.RewardRedemption', (message) => {
			const rewardName = getRewardName(message);
			debugLog('event rewardName=' + rewardName);

			const group = findGroupForReward(rewardName);

			if (!group) {
				debugLog('reward skipped: ' + rewardName);
				return;
			}

			debugLog('reward matched: ' + rewardName + ' group: ' + group.name);
			showNotification(group, message, rewardName);
		});

		attached = true;
		console.log('[reward-notifications] listener attached');
		return true;
	};

	if (attach()) return;

	waitForClient(60, 200, () => {
		attach();
	});
}

function waitForClient(maxAttempts, delayMs, onReady) {
	let attempt = 0;

	const timer = window.setInterval(() => {
		attempt++;

		if (window.client && typeof window.client.on === 'function') {
			window.clearInterval(timer);
			onReady();
			return;
		}

		if (attempt >= maxAttempts) {
			window.clearInterval(timer);
			console.warn('[reward-notifications] Streamerbot client is not ready');
		}
	}, delayMs);
}

function ensureContainer() {
	let existing = document.getElementById('reward-notification-container');

	if (existing) return existing;

	const node = document.createElement('div');
	node.id = 'reward-notification-container';
	document.body.appendChild(node);
	return node;
}

function injectStyles() {
	if (document.getElementById('reward-notification-styles')) return;

	const style = document.createElement('style');
	style.id = 'reward-notification-styles';
	style.textContent = `
    #reward-notification-container {
      position: fixed;
			top: 0;
			right: 0;
			bottom: 0;
			left: 0;
      pointer-events: none;
      z-index: 9999;
      display: flex;
      align-items: flex-end;
      justify-content: center;
      padding: 40px;
      box-sizing: border-box;
    }

    .reward-notification {
      position: absolute;
      left: 50%;
			bottom: ${CONFIG.ui.positionBottomPx}px;
      transform: translateX(-50%) translateY(24px) scale(0.96);
      opacity: 0;
      display: flex;
		flex-direction: column;
		align-items: center;
		gap: 12px;
		max-width: 860px;
		width: calc(100vw - 80px);
		padding: 0;
		border-radius: 0;
			background: transparent;
			border: 0;
			box-shadow: none;
			backdrop-filter: none;
      color: #f8fafc;
      font-family: "Segoe UI", Tahoma, sans-serif;
      overflow: hidden;
      animation: reward-notification-in 320ms ease-out forwards;
    }

    .reward-notification::before {
      content: '';
      position: absolute;
      inset: 0;
			background: transparent;
      pointer-events: none;
    }

    .reward-notification__image {
			width: auto;
			height: auto;
			max-width: ${CONFIG.ui.imageSizePx}px;
			max-height: ${CONFIG.ui.imageSizePx}px;
			flex: 0 0 auto;
      border-radius: 0;
      object-fit: contain;
			background: transparent;
			box-shadow: none;
    }

    .reward-notification__body {
      position: relative;
      z-index: 1;
      display: flex;
      flex-direction: column;
      gap: 8px;
      min-width: 0;
			align-items: center;
			text-align: center;
    }

    .reward-notification__title {
			font-size: ${CONFIG.ui.titleFontSizePx}px;
      line-height: 1.2;
      font-weight: 700;
      letter-spacing: 0.01em;
			word-break: break-word;
			text-shadow: ${CONFIG.ui.textShadow.offsetXPx}px ${CONFIG.ui.textShadow.offsetYPx}px ${CONFIG.ui.textShadow.blurPx}px rgba(0, 0, 0, ${CONFIG.ui.textShadow.opacity});
    }

		.reward-notification__tag {
			color: ${CONFIG.ui.tagColor};
			font-weight: 700;
		}

    .reward-notification__reward {
      display: inline-flex;
			align-self: center;
			padding: 0;
			border-radius: 0;
			background: transparent;
			font-size: ${CONFIG.ui.rewardTagFontSizePx}px;
      line-height: 1;
      text-transform: uppercase;
      letter-spacing: 0.12em;
			text-shadow: ${CONFIG.ui.textShadow.offsetXPx}px ${CONFIG.ui.textShadow.offsetYPx}px ${CONFIG.ui.textShadow.blurPx}px rgba(0, 0, 0, ${CONFIG.ui.textShadow.opacity});
    }

    .notification-card--chir {
			box-shadow: none;
    }

		.notification-card--video {
			box-shadow: none;
		}

    .reward-notification--leave {
      animation: reward-notification-out 260ms ease-in forwards;
    }

		.reward-notification--video {
			left: 0;
			right: 0;
			bottom: 0;
			width: 100vw;
			max-width: none;
			transform: none;
			animation: reward-video-in 180ms linear forwards;
		}

		.reward-notification--video.reward-notification--leave {
			animation: reward-video-out 180ms linear forwards;
		}

		.reward-notification__video {
			display: block;
			width: 100vw;
			height: 100vh;
			object-fit: contain;
			background: transparent;
		}

    @keyframes reward-notification-in {
      from {
        opacity: 0;
        transform: translateX(-50%) translateY(24px) scale(0.96);
      }
      to {
        opacity: 1;
        transform: translateX(-50%) translateY(0) scale(1);
      }
    }

    @keyframes reward-notification-out {
      from {
        opacity: 1;
        transform: translateX(-50%) translateY(0) scale(1);
      }
      to {
        opacity: 0;
        transform: translateX(-50%) translateY(18px) scale(0.98);
      }
    }

		@keyframes reward-video-in {
			from {
				opacity: 0;
			}
			to {
				opacity: 1;
			}
		}

		@keyframes reward-video-out {
			from {
				opacity: 1;
			}
			to {
				opacity: 0;
			}
		}
  `;

	document.head.appendChild(style);
}

class ImageRewardNotification {
	constructor(group, rewardName, textValues) {
		this.group = group;
		this.rewardName = rewardName;
		this.textValues = textValues;
	}

	render() {
		const card = document.createElement('div');
		card.className = `reward-notification ${this.group.className || ''}`.trim();

		const image = document.createElement('img');
		image.className = 'reward-notification__image';
		image.src = resolveAssetPath(this.group.image);
		image.onerror = () => {
			debugLog('image load failed: ' + this.group.image);
		};
		image.alt = this.rewardName;

		const body = document.createElement('div');
		body.className = 'reward-notification__body';

		const rewardTag = document.createElement('div');
		rewardTag.className = 'reward-notification__reward';
		rewardTag.innerHTML = formatTaggedHtml('{rewardName}', this.textValues);

		const title = document.createElement('div');
		title.className = 'reward-notification__title';
		title.innerHTML = formatTaggedHtml(this.group.textTemplate, this.textValues);

		body.appendChild(rewardTag);
		body.appendChild(title);
		card.appendChild(image);
		card.appendChild(body);

		return card;
	}

	afterMount(card) {
		const durationMs = this.group.durationMs || CONFIG.defaultDurationMs;
		window.setTimeout(() => dismissNotification(card), durationMs);
	}
}

class VideoRewardNotification {
	constructor(group, rewardName, selectionKey) {
		this.group = group;
		this.rewardName = rewardName;
		this.selectionKey = selectionKey;
		this.video = null;
		this.videoSource = '';
	}

	render() {
		const card = document.createElement('div');
		card.className = `reward-notification reward-notification--video ${this.group.className || ''}`.trim();

		const video = document.createElement('video');
		const hasExternalSound = typeof this.group.sound === 'string' && this.group.sound.trim() !== '';
		const shouldMuteVideo = typeof this.group.videoMuted === 'boolean' ? this.group.videoMuted : hasExternalSound;
		const videoSource = selectVideoSource(this.group.video, this.selectionKey);
		video.className = 'reward-notification__video';
		this.videoSource = videoSource;
		video.src = resolveAssetPath(videoSource);
		video.autoplay = true;
		video.muted = shouldMuteVideo;
		video.playsInline = true;
		video.preload = 'auto';
		video.loop = !!this.group.loopVideo;
		video.setAttribute('playsinline', 'playsinline');
		if (video.muted) {
			video.setAttribute('muted', 'muted');
		} else {
			video.removeAttribute('muted');
		}
		video.setAttribute('aria-label', this.rewardName);
		video.load();

		video.onloadeddata = () => {
			debugLog('video loaded: ' + this.videoSource + ' readyState=' + video.readyState);
		};

		video.oncanplay = () => {
			debugLog('video can play: ' + this.videoSource);
		};

		video.onerror = () => {
			debugLog('video load failed: ' + this.videoSource + ' networkState=' + video.networkState + ' readyState=' + video.readyState);
			window.setTimeout(() => dismissNotification(card), 300);
		};

		card.appendChild(video);
		this.video = video;

		return card;
	}

	afterMount(card) {
		if (this.video && typeof this.video.play === 'function') {
			const playPromise = this.video.play();
			if (playPromise && typeof playPromise.catch === 'function') {
				playPromise.catch((error) => {
					console.warn('Reward video playback failed:', error);
					debugLog('video play failed: ' + this.videoSource);
				});
			}
		}

		if (this.group.durationMs) {
			window.setTimeout(() => dismissNotification(card), this.group.durationMs);
			return;
		}

		if (!this.video || this.video.loop) {
			window.setTimeout(() => dismissNotification(card), CONFIG.defaultDurationMs);
			return;
		}

		this.video.onended = () => {
			dismissNotification(card);
		};
	}
}

const NOTIFICATION_RENDERERS = {
	image: ImageRewardNotification,
	video: VideoRewardNotification,
};

function createNotificationRenderer(group, rewardName, textValues, selectionKey) {
	const rendererKey = typeof group.renderer === 'string' ? group.renderer : 'image';
	const Renderer = NOTIFICATION_RENDERERS[rendererKey] || ImageRewardNotification;

	if (rendererKey === 'video') {
		return new Renderer(group, rewardName, selectionKey);
	}

	return new Renderer(group, rewardName, textValues);
}

function showNotification(group, message, rewardName) {
	const nickname = getNickname(message);
	const textValues = {
		nickname,
		rewardName,
	};

	const selectionKey = buildRewardSelectionKey(group, rewardName, textValues, message);
	const renderer = createNotificationRenderer(group, rewardName, textValues, selectionKey);
	const card = renderer.render();
	container.appendChild(card);

	playSound(group.sound);
	renderer.afterMount(card);
}

function dismissNotification(card) {
	if (!card || !card.isConnected) return;

	card.classList.add('reward-notification--leave');
	window.setTimeout(() => card.remove(), 300);
}

function playSound(soundPath) {
	if (!soundPath) return;

	const audio = new Audio(resolveAssetPath(soundPath));
	audio.volume = 1;
	audio.play().catch((error) => {
		console.warn('Reward sound playback failed:', error);
	});
}

function resolveAssetPath(path) {
	if (!path) return '';

	const value = String(path).trim();
	if (!value) return '';

	// Preserve fully-qualified URLs and data/blob URIs as-is.
	if (/^(https?:|data:|blob:|file:)/i.test(value)) {
		return value;
	}

	// Encode local relative paths so old CEF can open files with non-ASCII names.
	return encodeURI(value);
}

function selectVideoSource(video, selectionKey) {
	if (Array.isArray(video)) {
		const candidates = video
			.map((item) => String(item || '').trim())
			.filter((item) => item !== '');

		if (!candidates.length) return '';

		const selectedIndex = getDeterministicIndex(selectionKey, candidates.length);
		debugLog('video pick index=' + selectedIndex + ' key=' + selectionKey);
		return candidates[selectedIndex];
	}

	return String(video || '').trim();
}

function getDeterministicIndex(seed, length) {
	if (!length || length <= 1) return 0;

	const normalizedSeed = String(seed || '').trim();
	if (!normalizedSeed) {
		return Math.floor(Math.random() * length);
	}

	const hash = hashStringToUint32(normalizedSeed);
	return hash % length;
}

function hashStringToUint32(value) {
	let hash = 2166136261;

	for (let i = 0; i < value.length; i++) {
		hash ^= value.charCodeAt(i);
		hash = Math.imul(hash, 16777619);
	}

	return hash >>> 0;
}

function buildRewardSelectionKey(group, rewardName, textValues, message) {
	const data = message && message.data ? message.data : null;

	const redemptionId = firstNonEmpty([
		asNonEmptyString(data && data.redemption && data.redemption.id),
		asNonEmptyString(data && data.redemptionId),
		asNonEmptyString(data && data.id),
		asNonEmptyString(data && data.eventId),
		asNonEmptyString(data && data.messageId),
		asNonEmptyString(message && message.id),
	]);

	const redemptionTimestamp = firstNonEmpty([
		asNonEmptyString(data && data.redemption && data.redemption.redeemedAt),
		asNonEmptyString(data && data.redemption && data.redemption.redeemed_at),
		asNonEmptyString(data && data.redeemedAt),
		asNonEmptyString(data && data.timestamp),
		asNonEmptyString(data && data.ts),
		asNonEmptyString(data && data.createdAt),
		asNonEmptyString(data && data.redemption && data.redemption.createdAt),
		asNonEmptyString(message && message.timeStamp),
	]);

	const payloadAnchor = firstNonEmpty([
		redemptionId,
		redemptionTimestamp,
		stableSerialize(message),
	]);

	const rewardText = (textValues && textValues.rewardName) || rewardName || '';
	const groupName = group && group.name ? group.name : '';

	debugLog('selection anchor source=' + (redemptionId ? 'id' : (redemptionTimestamp ? 'timestamp' : 'payload')));

	return [
		groupName,
		rewardText,
		payloadAnchor,
	].join('|');
}

function asNonEmptyString(value) {
	if (value === null || typeof value === 'undefined') return '';
	const str = String(value).trim();
	return str;
}

function stableSerialize(value) {
	if (value === null || typeof value === 'undefined') return '';

	if (Array.isArray(value)) {
		return '[' + value.map((item) => stableSerialize(item)).join(',') + ']';
	}

	if (typeof value === 'object') {
		const keys = Object.keys(value).sort();
		return '{' + keys.map((key) => JSON.stringify(key) + ':' + stableSerialize(value[key])).join(',') + '}';
	}

	return JSON.stringify(value);
}

function findGroupForReward(rewardName) {
	const normalizedReward = normalize(rewardName);
	if (!normalizedReward) return null;

	const matchedGroup = findGroupByRewardMatch(normalizedReward);
	if (!matchedGroup) {
		debugLog('reward candidates miss for ' + normalizedReward + ': ' + describeRewardCandidates());
	}

	return matchedGroup;
}

function findGroupByRewardMatch(normalizedReward) {
	return CONFIG.groups.find((group) => {
		const candidates = Array.isArray(group.rewards) ? group.rewards : [];

		return candidates.some((candidate) => {
			const normalizedCandidate = normalize(candidate);
			if (!normalizedCandidate || !normalizedReward) return false;

			if (normalizedCandidate === normalizedReward) return true;

			return false;
		});
	});
}

function describeRewardCandidates() {
	return CONFIG.groups.map((group) => {
		const candidates = Array.isArray(group.rewards) ? group.rewards.map((candidate) => normalize(candidate)).join('|') : '';
		return group.name + '=' + candidates;
	}).join('; ');
}

function getRewardName(message) {
	const data = message && message.data ? message.data : null;

	const rewardTitle = data && data.reward && data.reward.title ? data.reward.title : '';
	const rewardName = data && data.reward && data.reward.name ? data.reward.name : '';
	const rewardAltName = data && data.reward && data.reward.rewardName ? data.reward.rewardName : '';
	const title = data && data.title ? data.title : '';
	const rewardTitleFlat = data && data.rewardTitle ? data.rewardTitle : '';
	const rewardNameFlat = data && data.rewardName ? data.rewardName : '';

	const resolved = firstNonEmpty([
		rewardTitle,
		rewardName,
		rewardAltName,
		title,
		rewardTitleFlat,
		rewardNameFlat,
	]);

	return resolved || 'Reward';
}

function getNickname(message) {
	const data = message && message.data ? message.data : null;

	const nickname = firstNonEmpty([
		data && data.user && data.user.displayName,
		data && data.user && data.user.name,
		data && data.user && data.user.username,
		data && data.user && data.user.login,
		data && data.userName,
		data && data.username,
		data && data.displayName,
		data && data.login,
		data && data.user_login,
		data && data.user_name,
		data && data.redemption && data.redemption.user && data.redemption.user.display_name,
		data && data.redemption && data.redemption.user && data.redemption.user.login,
		data && data.redemption && data.redemption.user && data.redemption.user.name,
		data && data.redemption && data.redemption.user && data.redemption.user.username,
	]);

	if (!nickname) {
		const keys = data ? Object.keys(data).join(',') : 'no-data';
		debugLog('nickname fallback to unknown; data keys=' + keys);
	}

	return nickname || 'unknown';
}

function formatTemplate(template, values) {
	return template
		.split('{nickname}').join(values.nickname)
		.split('{rewardName}').join(values.rewardName);
}

function formatTaggedHtml(template, values) {
	const text = formatTemplate(template, values);

	const escapedText = escapeHtml(text);
	const escapedNickname = escapeHtml(values.nickname);
	const escapedRewardName = escapeHtml(values.rewardName);

	return escapedText
		.split(escapedNickname).join('<span class="reward-notification__tag">' + escapedNickname + '</span>')
		.split(escapedRewardName).join('<span class="reward-notification__tag">' + escapedRewardName + '</span>');
}

function escapeHtml(value) {
	return String(value)
		.split('&').join('&amp;')
		.split('<').join('&lt;')
		.split('>').join('&gt;')
		.split('"').join('&quot;')
		.split("'").join('&#39;');
}

function firstNonEmpty(values) {
	for (let i = 0; i < values.length; i++) {
		const value = values[i];
		if (typeof value === 'string' && value.trim()) {
			return value;
		}
	}

	return '';
}

function normalize(value) {
	const str = String(value || '').trim().toLowerCase();

	// Remove duplicate spaces and surrounding quotes/punctuation often present in custom reward names.
	return str
		.replace(/\s+/g, ' ')
		.replace(/^[-–—"'`\s]+/, '')
		.replace(/[-–—"'`\s]+$/, '');
}

function debugLog(message) {
	if (!CONFIG.debug) return;
	console.log('[reward-notifications] ' + message);
}